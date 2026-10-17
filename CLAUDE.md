# CLAUDE.md — Backend Engineering Instructions

## Project context

This is the backend for the Chubb APAC Fullstack Developer take-home assessment: a claims management platform covering the motor/property claims lifecycle (submission → assignment → information requests → decision) for two user types, claimants and claims officers, sharing one Angular frontend (separate repo: `claim-management-system-fe`).

Constraints: sprint-format assessment, 2–3h target / 5h hard cap. Build a focused vertical slice, not a complete platform — correctness, security, testability, and architectural clarity outweigh feature count. Backend gets the majority of time; it is the graded foundation.

Approved stack: ASP.NET Core Web API on **.NET 10**, EF Core + **SQLite**, xUnit + coverlet, Kafka via **Confluent.Kafka** (local native KRaft broker in this environment — Docker is not installed on the dev machine; see `docs/ARCHITECTURE.md` and `README.md` for the exact run instructions). No Docker dependency in CI or ordinary tests.

MVP: claim submission with a unique reference, claimant tracking + info-request response, officer queue/assign/assess/decide, full status history, server-enforced ownership and role authorization. Deferred (see `docs/ASSUMPTIONS.md`): operational dashboard, real IdP, transactional outbox, attachments, multi-currency.

## Architecture rules

Modular monolith, Clean Architecture layering, dependency direction flows inward only:

`Api → Application → Domain` and `Infrastructure → Application, Domain`. **Domain must never reference EF Core, ASP.NET, or Confluent.Kafka.** Application defines interfaces (`IClaimRepository`, `IEventPublisher`, `IDateTimeProvider` etc.); Infrastructure implements them.

- **Domain**: `Claim` aggregate, claim state machine, status transition rules, domain exceptions. No I/O.
- **Application**: use-case services (one per workflow: submit, assign, request-info, respond-info, decide), DTOs, ownership/authorization checks that don't belong in HTTP middleware.
- **Infrastructure**: EF Core `ClaimsDbContext`, entity configurations, migrations, `KafkaEventPublisher`, the notification `BackgroundService` consumer.
- **Api**: controllers, mock JWT issuance, centralized exception → ProblemDetails middleware, OpenAPI.

Approved patterns: DI everywhere, narrow repository per aggregate root (`Claim`, not generic `IRepository<T>`), domain state-machine for transitions, versioned Kafka event contracts. **Do not introduce CQRS, MediatR, or a generic repository** — the use-case count doesn't justify the indirection; application services are called directly from controllers.

REST vs Kafka: REST for everything requiring an immediate response (submission, retrieval, assignment, decisions, dashboard queries). Kafka for the 5 lifecycle events (`ClaimSubmitted`, `ClaimAssigned`, `AdditionalInformationRequested`, `ClaimApproved`, `ClaimRejected`) — fire-and-log after the DB commit, never on the request's critical path, never required for an API call to succeed.

## Coding standards

- Nullable reference types **on** everywhere (`Directory.Build.props` treats nullable warnings as errors) — do not suppress with `!` unless truly provably non-null.
- Async/await for all I/O; propagate `CancellationToken` through service/repository method signatures.
- PascalCase types/members, camelCase locals/params, `_camelCase` private fields (enforced via `.editorconfig`).
- Every write endpoint binds to a dedicated request DTO — never bind directly to an EF entity.
- Centralized exception middleware maps domain/validation exceptions to RFC7807 `ProblemDetails`; no raw stack traces outside `Development`.
- `ILogger<T>` structured logging; log claim reference + user id, never incident free-text or secrets.
- No magic strings for status/role values — use the Domain enums.

## Security standards

- Dev-only JWT issuance via `/api/auth/dev-login` — **explicitly not production authentication**. No password, no real identity verification. Must not be reachable from a `Production` ASP.NET environment without a loud warning; document this limitation in README.
- `[Authorize(Roles = "...")]` on every controller action; role alone is not enough — **every claimant/officer-scoped endpoint must also verify ownership/assignment in the Application layer**, independent of any frontend route guard.
- Signing key from user-secrets/environment variable, never committed.
- SQL injection is structurally prevented by EF Core parameterization — no raw SQL string concatenation.
- CORS restricted to the known Angular dev origin.

## Testing rules

- xUnit, plain `Assert.*` (no FluentAssertions — its v8+ license isn't appropriate to pull into a company assessment).
- Domain: every valid and invalid state transition gets a test.
- Application: ownership enforcement, concurrent-assignment race, info-request/response cycle, decision rules.
- Integration: `WebApplicationFactory` for API tests, isolated SQLite per test collection (never share a DB file across tests, never touch a real `claims.db`).
- Ordinary `dotnet test` must never require Kafka or Docker.
- Coverage via `coverlet.collector`, target ~80% line coverage on Domain+Application — report the actual number, don't assume it.

## Development workflow

- Plan before coding non-trivial changes; this file plus `docs/ARCHITECTURE.md` are the standing plan — update them when a real decision changes, don't let them drift stale.
- Build (`dotnet build`) and run relevant tests after every meaningful change, before committing.
- Update `AI_WORKING_JOURNAL.md` as you go — not retroactively.
- Flag before pushing to `origin` (pushing is shared/visible state); local commits after each verified milestone are fine without asking each time.

## Git rules

- Small, logical commits per verified milestone, not one giant commit.
- Never fabricate history, test results, coverage numbers, or CI outcomes.
- No secrets, no `claims.db`, no `bin/`/`obj/` in commits (see `.gitignore`).
