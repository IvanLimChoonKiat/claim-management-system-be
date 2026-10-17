# Architecture

## System overview

A modular monolith ASP.NET Core Web API serving a single Angular frontend (separate repo). Single bounded context (claims lifecycle) — not split into microservices. Synchronous reads/writes go over REST; the 5 claim-lifecycle transitions are additionally published as Kafka events for downstream consumers.

```
Angular (separate repo)
        │  REST (JWT bearer)
        ▼
┌─────────────────────────────────────────────┐
│ ClaimsManagement.Api                         │
│  controllers, mock auth, ProblemDetails      │
│  exception middleware, OpenAPI               │
└───────────────┬───────────────────────────────┘
                 ▼
┌─────────────────────────────────────────────┐
│ ClaimsManagement.Application                 │
│  use-case services, DTOs, ownership checks   │
│  IClaimRepository / IEventPublisher (ports)  │
└───────┬───────────────────────────┬───────────┘
        ▼                           ▼
┌───────────────────┐   ┌─────────────────────────────┐
│ ClaimsManagement   │   │ ClaimsManagement             │
│ .Domain            │   │ .Infrastructure               │
│ Claim aggregate,    │   │ EF Core + SQLite,            │
│ state machine,      │   │ KafkaEventPublisher,          │
│ invariants           │   │ Notification consumer         │
│ (no EF/HTTP/Kafka)   │   │ (implements Application ports)│
└───────────────────┘   └─────────────────────────────┘
                                     │
                                     ▼
                            Kafka (local KRaft broker)
```

Dependencies point inward: `Api → Application → Domain`, `Infrastructure → Application, Domain`. Domain has zero references to EF Core, ASP.NET, or Confluent.Kafka — it is pure C#, independently testable.

## Why a modular monolith (not microservices)

Single team, single bounded context, 2–3h build budget. Splitting into services would add network calls, distributed transactions, and deployment complexity with no corresponding benefit at this scope. The module boundaries (Domain/Application/Infrastructure/Api) are drawn so that extraction into separate services later — if claim intake and claim assessment ever needed independent scaling/ownership — would follow existing seams rather than requiring a rewrite.

## Why Clean Architecture, pragmatically

Only three layers are enforced strictly (Domain has no outward dependencies); Application and Infrastructure are kept small and direct rather than maximally abstracted. No MediatR, no CQRS, no generic repository — the number of use cases here (≈8) doesn't justify indirection that would only pay off at much larger scale. One narrow repository interface per aggregate root (`IClaimRepository`), not a generic `IRepository<T>`.

## Domain model

- **User** — Id, Email, DisplayName, Role (`Claimant` | `ClaimsOfficer`)
- **Claim** (aggregate root) — Id, ClaimReference (unique, `CLM-YYYY-NNNNNN`), Type (`Motor`|`Property`), ClaimantUserId, Status, IncidentDateUtc, IncidentDescription, IncidentLocation, type-specific nullable fields (VehicleRegistration / PropertyAddress — flat columns rather than table-per-type inheritance; deliberate simplification, see Trade-offs), AssignedOfficerUserId?, EstimatedLiability?, CreatedAtUtc, UpdatedAtUtc, Version (optimistic concurrency token)
- **ClaimStatusHistory** — audit trail of every transition: FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc, Note?
- **InformationRequest** — RequestedByOfficerUserId, RequestText, ResponseText?, Status
- **ClaimDecision** — OfficerUserId, Outcome (`Approved`|`Rejected`), SettlementAmount?, Reason, DecidedAtUtc
- **NotificationLog** — EventId (idempotency key), ClaimId, EventType, CreatedAtUtc — written by the Kafka consumer

### Claim state machine

```
Submitted --assign(officer)--> Assigned
Assigned  --requestInfo(officer)--> InfoRequested
InfoRequested --respond(claimant)--> Assigned
Assigned  --approve(officer)--> Approved   [terminal]
Assigned  --reject(officer)--> Rejected    [terminal]
```

Owned entirely by the Domain layer (a method on `Claim`, e.g. `Claim.Assign(officerId)`), which throws `InvalidClaimTransitionException` on an illegal transition. The Api layer maps that to `409 Conflict`. Only the Domain decides what transitions are legal; Application decides *who* is allowed to invoke them (ownership/role checks).

## REST vs Kafka

REST handles everything the caller needs an immediate answer for: submission, retrieval, assignment, info-request/response, decisions, dashboard queries. Kafka carries the 5 lifecycle events (`ClaimSubmitted`, `ClaimAssigned`, `AdditionalInformationRequested`, `ClaimApproved`, `ClaimRejected`) for anything that can happen *after* the response is already sent — notifications, audit feeds, future downstream consumers (e.g. a reporting service). No API call's success depends on Kafka being up.

### Event contracts

Versioned JSON envelope, one topic per event type, keyed by `claimId` for per-claim ordering:

```json
{
  "eventId": "guid",
  "eventType": "ClaimSubmitted",
  "version": 1,
  "occurredAtUtc": "2026-10-17T08:00:00Z",
  "claimId": "guid",
  "payload": { "...event-specific fields..." }
}
```

### Delivery guarantees, retries, idempotency

Producer: `acks=all`, published **after** the owning DB transaction commits, from a thin `IEventPublisher` port implemented in Infrastructure. A publish failure is logged and swallowed — it never fails the HTTP request or rolls back the DB write. This is a deliberate **at-least-once, no transactional outbox** choice (see Trade-offs).

Consumer: the notification `BackgroundService` is idempotent — it keys on `eventId` and no-ops on a row that already exists in `NotificationLog`, so Kafka re-delivery (at-least-once semantics) never double-processes.

Ordering: guaranteed per-claim because all 5 topics are keyed by `claimId`, so a given claim's events land on the same partition in publish order. No ordering guarantee *across* different claims, which is fine — nothing in this domain needs it.

Kafka unavailable: the API keeps working; events are simply not published for that window. This is an accepted gap for an assessment-scope system, not something a production system should accept — see Trade-offs.

## SQLite design

EF Core, Code-First migrations. Unique index on `ClaimReference`; indexes on `Status` and `AssignedOfficerUserId` for the officer-queue/workload queries. UTC timestamps throughout (`DateTimeOffset` or UTC `DateTime` with an `IDateTimeProvider` abstraction so tests can control time). Money as `decimal` with `HasPrecision(18,2)`.

Concurrency: SQLite has no native rowversion/xmin mechanism, so an application-level `Version` integer column (`[ConcurrencyCheck]`/`IsConcurrencyToken`) guards claim status transitions — two officers racing to assign the same claim will have one succeed and one get a concurrency exception mapped to `409 Conflict`, rather than silently overwriting.

Test isolation: every test collection gets its own SQLite database (temp file or a scoped in-memory connection kept open for the test's lifetime) — never a shared file, never the real `claims.db`.

## Security architecture

Mock JWT issuance (`POST /api/auth/dev-login`) for the assessment — **not production authentication**, documented loudly in README. `[Authorize(Roles=...)]` per endpoint plus an independent ownership/assignment check in the Application layer (so a forged or buggy Angular route guard can never expose another claimant's data). Every write endpoint uses a dedicated request DTO (mass-assignment prevention). EF Core parameterized queries prevent SQL injection by construction. CORS restricted to the Angular dev origin. Centralized exception middleware returns RFC7807 ProblemDetails, no stack traces outside `Development`. Structured logs never include incident free-text or secrets.

## Trade-offs (what would change for production)

| Decision here | Production alternative |
|---|---|
| SQLite | PostgreSQL/SQL Server — concurrent write throughput, proper backup/HA |
| No transactional outbox | Outbox table + relay (e.g. Debezium or a polling publisher) for exactly-the-DB-commits-and-the-event-ships-or-neither guarantee |
| Mock JWT auth | Real IdP (Azure AD / Auth0 / Chubb's own) with password or SSO |
| Flat Claim entity for Motor/Property fields | Table-per-type or a JSON sidecar if claim types diverge further |
| Local/native Kafka broker | Managed Kafka (Confluent Cloud / MSK) or at least a properly orchestrated Docker/K8s broker |
