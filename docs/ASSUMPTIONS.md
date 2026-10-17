# Assumptions

## Business assumptions

- Single currency/locale. The brief mentions six APAC markets; multi-currency/multi-locale handling is out of scope for the time budget and would be a straightforward extension (currency code on `Claim`, formatting in Angular) rather than an architectural change.
- An officer can only have one claim assigned at a time to a given stage, but no cap on total workload is enforced — "see team's workload" is read-only visibility, not capacity management.
- Claim submission requires an authenticated claimant (mock-authenticated for this assessment). No anonymous/guest submission flow.
- "Additional information" is a free-text request/response cycle, not a structured document-upload flow.
- Settlement amount is a single decimal figure recorded at decision time; no itemized settlement breakdown, no actual payment/disbursement integration.

## Technical assumptions

- .NET 10 and the installed Kafka 4.3.1/JDK 21 combination are acceptable even though they're newer than what a typical "supported SDK" framing might assume — chosen because they're what's actually installed and current.
- SQLite is acceptable as the assessment's persistence layer per the brief's "database... all your decisions" — explicitly not the production recommendation (see `docs/ARCHITECTURE.md` trade-offs).
- Running Kafka against a native local broker (not Docker) is acceptable given Docker isn't installed on this development machine; `docker-compose.yml` is provided but unverified here.
- CI runs on `ubuntu-latest` GitHub-hosted runners without Docker/Kafka — ordinary tests must pass without a broker.

## MVP boundaries

In scope: claim submission, claimant tracking + info-request response, officer queue/assign/assess/decide, status history, Kafka events for the 5 lifecycle transitions with one real consumer, SQLite persistence with migrations, mock auth with server-enforced ownership, xUnit coverage on Domain+Application, `verify.ps1` end-to-end smoke test, CI in both repos, Angular UI for both roles including a team-wide operational dashboard (claims by status, outstanding liability exposure, per-officer workload/performance).

**Role scoping for the dashboard:** the brief's capability list defines only Claimant and ClaimsOfficer; "Manager" appears solely in the background narrative ("Managers have no real-time picture..."), not as a distinct login with its own capabilities. `GET /api/dashboard/summary` is therefore visible to any ClaimsOfficer rather than gated behind an invented third role — this satisfies "officer sees the entire team's workload" literally and serves the manager-narrative's need through the same view, without adding role/auth surface the brief didn't ask for.

**"Performance" definition:** per-officer decision counts (active/approved/rejected/total handled) — the simplest honest read of "team performance reporting" this scope supports. Cycle-time/SLA metrics would need `UpdatedAtUtc` deltas per status transition, which the status-history table has but isn't aggregated here; a reasonable next increment, not attempted.

## Deferred functionality (explicitly out of scope, not silently dropped)

- Real identity provider / password-based auth.
- Transactional outbox for Kafka publishing (documented trade-off instead).
- File/photo attachments on claims.
- Multi-currency / multi-market handling.
- Angular visual polish — functional only, no design system.
- Rate limiting, API versioning, horizontal scaling concerns.
