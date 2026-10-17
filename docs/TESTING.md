# Testing

Strategy drafted in Phase 2 before test code existed; "Actual results" below reflects the real Phase 4 run.

## Strategy

| Layer | Tool | What's covered |
|---|---|---|
| Domain | xUnit | Every valid and invalid claim state transition; claim reference generation; invariants |
| Application | xUnit | Use-case services (submit, assign, request-info, respond, decide); ownership enforcement; concurrent-assignment race; validation |
| Api + persistence | xUnit + `WebApplicationFactory` + isolated SQLite file per test class | End-to-end HTTP tests for the critical paths (submit → assign → request-info → respond → approve, plus negative cases) — this is also where EF Core mapping, constraints, and migrations get exercised against a real database; there is no separate repository-only test file (see "Known gaps") |
| Kafka | Manual/opt-in | Event serialization round-trip tested without a broker; live produce/consume verified via `scripts/kafka-start.ps1` + `verify.ps1 -WithKafka`, not part of ordinary `dotnet test` |

## Critical scenarios (from the engineering brief)

1. Valid claim creation
2. Invalid claim submission (validation failures)
3. Valid claim status transitions
4. Invalid claim status transitions (rejected, mapped to 409)
5. Claim assignment
6. Duplicate/concurrent assignment (optimistic concurrency)
7. Claim ownership enforcement (claimant can't see/act on another claimant's claim)
8. Approval and rejection rules
9. Additional information workflow (request → respond → back to Assigned)
10. Outstanding liability calculation (if the dashboard stretch goal is reached)
11. Persistence behavior (constraints, indexes, migrations)
12. Event serialization (Kafka envelope round-trips correctly)

## Commands

```bash
dotnet test ClaimsManagement.sln --collect:"XPlat Code Coverage"
```

Coverage report lands under `TestResults/<run-id>/coverage.cobertura.xml`. CI uploads it as a build artifact (see `.github/workflows/ci.yml`).

## Test isolation

Every integration/persistence test collection creates its own SQLite database (temp file or scoped in-memory connection) — never a shared file, never the real `claims.db`. Ordinary unit tests have no I/O at all.

## Coverage target

~80% line coverage on Domain + Application, which carry the business rules the brief asks to be tested. Coverage is a signal, not a goal in itself — branch coverage on the state machine and ownership checks matters more than the raw percentage.

## Known gaps

- **Kafka production/consumption over a live broker is not covered by `dotnet test`.** `KafkaEventPublisherTests` exercises the event → topic/envelope mapping for all 5 lifecycle events with Kafka disabled (no network call), which is what ordinary unit tests can safely do. The actual produce/consume path against a running broker is verified separately via `scripts/verify.ps1 -WithKafka` (Phase 5), not by the automated test suite — consistent with "ordinary unit tests must not depend on Kafka."
- **`NotificationConsumerBackgroundService`** (the Kafka consumer) has low direct coverage (~17%) for the same reason — its core logic (idempotent `NotificationLog` write) only really exercises against a live broker.
- **`ClaimRepository`'s query methods** are covered where exercised through a real API call (`GetByIdAsync`, `GetUnassignedAsync`, `GetAssignedToOfficerAsync`); a couple of unused-by-any-current-endpoint methods (`GetByReferenceAsync`, `GetAllAsync`) are not directly tested — kept because they're small, obviously-correct LINQ, and exist for the dashboard stretch goal.
- No dedicated outstanding-liability-calculation tests exist, because the operational dashboard (brief's "if time permits" item) was not reached — see `docs/ASSUMPTIONS.md`.
- Concurrent assignment is tested at the Application layer with a fake `IUnitOfWork` that simulates `ConcurrentUpdateException` (`ClaimAssignmentServiceTests.AssignToOfficerAsync_OnConcurrentAssignmentConflict_ThrowsConcurrentUpdateException`), not via two real concurrent EF Core `SaveChanges` calls racing against SQLite — a true race-condition integration test was judged lower value than the time it would cost for this assessment's budget.

## Actual results

As of the Phase 4 (testing and quality) commit:

- **54 tests executed, 54 passed, 0 failed, 0 skipped** (`dotnet test ClaimsManagement.sln`)
- **Line coverage: 89.3%** overall (1514/1695 coverable lines) — **Domain 89.5%**, **Application 90.7%**, both above the ~80% target on the layers that carry business rules
- **Branch coverage: 68.6%** (92/134) — lower than line coverage mainly because of untaken Kafka-disabled/error-handling branches noted above
- **Method coverage: 92.6%** (251/271)
- Coverage report: `TestResults/<run-id>/coverage.cobertura.xml` (Cobertura format, consumed by CI and by `reportgenerator` for the human-readable summary above)
- Test breakdown: `Domain/ClaimTests.cs` (21 tests, every valid/invalid state transition), `Domain/ClaimReferenceGeneratorTests.cs` (2), `Application/*ServiceTests.cs` (18, covering submission, assignment incl. simulated concurrency conflict, information requests, decisions, ownership-scoped queries), `Infrastructure/KafkaEventPublisherTests.cs` (5, one per lifecycle event type), `Api/ClaimsApiTests.cs` (8, full HTTP lifecycle against a real isolated SQLite database via `WebApplicationFactory`)
