# Testing

_Initial strategy, written during Phase 2 before test code exists. Updated with actual results once Phase 4 (testing and quality) completes — see "Actual results" at the bottom._

## Strategy

| Layer | Tool | What's covered |
|---|---|---|
| Domain | xUnit | Every valid and invalid claim state transition; claim reference generation; invariants |
| Application | xUnit | Use-case services (submit, assign, request-info, respond, decide); ownership enforcement; concurrent-assignment race; validation |
| Infrastructure/persistence | xUnit + isolated SQLite | EF Core mapping, constraints, migrations apply cleanly |
| Api | xUnit + `WebApplicationFactory` | A handful of end-to-end HTTP tests for the critical paths (submit → assign → decide) |
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

## Known gaps (updated honestly as of each phase)

_Filled in once tests exist — see "Actual results" below for the real numbers, not a promise made in advance._

## Actual results

_Not yet available — Phase 4 not started. This section will report tests executed/passed/failed/skipped and the real coverage percentage once that phase completes, including any untested critical areas._
