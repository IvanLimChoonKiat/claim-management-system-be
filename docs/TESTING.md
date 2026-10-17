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

## Functional verification

`scripts/verify.ps1` (`scripts/verify.sh` cross-platform companion) is a separate layer from `dotnet test` — it builds Release, runs the test suite, then starts a real instance of the API against an isolated temp SQLite database and drives the actual REST lifecycle via `Invoke-RestMethod`/`curl`: dev-login, submit, retrieve, assign, a valid decision transition, and a rejected invalid transition (re-deciding an already-decided claim, asserting 409). `-WithKafka`/`--with-kafka` additionally checks the local broker is reachable; omitted by default so ordinary verification never requires Kafka.

**Actually run, both modes:**
```
./scripts/verify.ps1            -> All checks passed. (10/10)
./scripts/verify.ps1 -WithKafka -> All checks passed. (11/11, including live Kafka connectivity)
```

## Test isolation

Every integration/persistence test collection creates its own SQLite database (temp file or scoped in-memory connection) — never a shared file, never the real `claims.db`. Ordinary unit tests have no I/O at all.

## Coverage target

~80% line coverage on Domain + Application, which carry the business rules the brief asks to be tested. Coverage is a signal, not a goal in itself — branch coverage on the state machine and ownership checks matters more than the raw percentage.

## Known gaps

- **Kafka production/consumption over a live broker is not covered by `dotnet test`.** `KafkaEventPublisherTests` exercises the event → topic/envelope mapping for all 5 lifecycle events with Kafka disabled (no network call), which is what ordinary unit tests can safely do. The actual produce/consume path against a running broker is verified separately via `scripts/verify.ps1 -WithKafka` (Phase 5), not by the automated test suite — consistent with "ordinary unit tests must not depend on Kafka."
- **`NotificationConsumerBackgroundService`** (the Kafka consumer) has low direct coverage (~17%) for the same reason — its core logic (idempotent `NotificationLog` write) only really exercises against a live broker.
- **`ClaimRepository`'s query methods** are covered where exercised through a real API call (`GetByIdAsync`, `GetUnassignedAsync`, `GetAssignedToOfficerAsync`); a couple of unused-by-any-current-endpoint methods (`GetByReferenceAsync`, `GetAllAsync`) are not directly tested — kept because they're small, obviously-correct LINQ, and exist for the dashboard stretch goal.
- Concurrent assignment is tested at the Application layer with a fake `IUnitOfWork` that simulates `ConcurrentUpdateException` (`ClaimAssignmentServiceTests.AssignToOfficerAsync_OnConcurrentAssignmentConflict_ThrowsConcurrentUpdateException`), not via two real concurrent EF Core `SaveChanges` calls racing against SQLite — a true race-condition integration test was judged lower value than the time it would cost for this assessment's budget.
- Outstanding-liability aggregation is covered (`DashboardServiceTests`, `ClaimsApiTests.DashboardSummary_...`), but there's no dedicated test for liability precision/rounding at scale (hundreds of claims) — the summed `decimal` arithmetic is straightforward enough that this was judged low-value to test separately.

## Actual results

As of the dashboard addition (post-Phase-7):

- **60 tests executed, 60 passed, 0 failed, 0 skipped** (`dotnet test ClaimsManagement.sln`)
- **Line coverage: 89.4%** overall (1572/1757 coverable lines) — Domain and Application both still comfortably above the ~80% target
- **Branch coverage: 69.1%** (94/136)
- Coverage report: `TestResults/<run-id>/coverage.cobertura.xml` (Cobertura format, consumed by CI and by `reportgenerator` for the human-readable summary above)
- Test breakdown: `Domain/ClaimTests.cs` (21), `Domain/ClaimReferenceGeneratorTests.cs` (2), `Application/*ServiceTests.cs` (22, incl. `DashboardServiceTests`), `Infrastructure/KafkaEventPublisherTests.cs` (5), `Api/ClaimsApiTests.cs` (10, incl. dashboard summary + role-gating)
