# AI Working Journal

Real log of AI collaboration on this assessment. Not polished — kept current as work happens, not reconstructed afterward.

---

## 2026-10-17 — Phase 1: Architecture planning

**Prompt:** User supplied a detailed operating brief (`# CHUBB APAC — FULLSTACK ENGINEERIN.txt`) plus the official assessment PDF, and asked for a repository-aware architecture proposal before any code — explicitly: do not generate application code yet.

**AI actions:** Read the PDF (authoritative requirements — motor/property claims lifecycle, claimant + officer flows, REST+Kafka split left to candidate, SQLite/DB left to candidate, .NET or Java left to candidate, 2–3h target/5h hard cap). Inspected both repos: `claim-management-system-be` and `claim-management-system-fe`, each a single README-only commit with `origin` already pointing at the correct GitHub URLs — nothing to preserve. Checked local tooling: .NET SDKs 9.0.318/10.0.400, Node 22.18, **no Docker installed**, found a pre-installed native Kafka 4.3.1 distribution at `C:\kafka` and a Temurin JDK 21 (system default Java is 1.8, too old for Kafka 4.x).

**AI recommendation:** Three Kafka options presented — (a) write the Kafka code but don't run it live, (b) install Docker Desktop mid-session, (c) skip Kafka entirely and document why.

**Human decision:** None of the three — user pointed out Kafka was already installed locally and said to run against that instead ("use the local kafka"). **Overrode the AI's framing of the question** (AI hadn't checked for a pre-existing local Kafka install before asking). AI then verified the local install's version/Java requirements and confirmed JDK 21 was available to satisfy them.

**AI recommendation:** A condensed architecture plan (domain model, REST contract, Kafka topics, security, testing targets, CI, time-boxed phases) synthesized from the operating brief.

**Human decision:** **Challenged** — approved proceeding, but required following the full original operating `.txt` file's exact structure/wording throughout (role, phase numbering, exact `src/ClaimsManagement.*` repo layout) rather than the AI's paraphrase of it, to avoid drift from specifics the user had already decided. Plan was revised to explicitly defer to that file and re-presented; approved.

**Outcome:** Plan approved as revised. Implementation started with Phase 2 (engineering foundation).

---

## 2026-10-17 — Phase 2: Engineering foundation

**AI actions:** Scaffolded `ClaimsManagement.sln` with the 4 `src/` projects + `tests/ClaimsManagement.Tests` per the approved layout, wired project references (Domain ← Application ← Infrastructure/Api, Tests → all), added EF Core Sqlite + Design, Confluent.Kafka, JwtBearer, Swashbuckle, Mvc.Testing, coverlet.collector packages.

**AI decision (not escalated, logged for transparency):** Initially added `FluentAssertions` to the test project for assertion ergonomics, then removed it — FluentAssertions v8+ carries a commercial license that isn't appropriate to pull into a company take-home submission without sign-off. Switched to plain xUnit `Assert`. This is a minor, easily-reversible call; flagging here rather than treating it as silent.

**Verification:** `dotnet build` succeeds (0 warnings, 0 errors) with the empty scaffold before any domain code was added.

---

## 2026-10-17 — Phase 3: Backend implementation

**AI actions:** Implemented the `Claim` aggregate (state machine, domain events, ownership/transition guards), Application use-case services, EF Core persistence (DbContext, configurations, repositories, optimistic-concurrency `UnitOfWork`), Kafka producer/consumer, and the Api layer (controllers, mock JWT auth, centralized ProblemDetails exception handling, Swagger).

**Verification approach:** Rather than just compiling and declaring done, ran the API for real (`dotnet run`) and drove the full claim lifecycle through actual HTTP calls with `curl` - dev-login as claimant/officer, submit, assign, request info, respond, approve, plus negative cases (invalid transition, wrong role, wrong owner, missing-field validation, domain validation). This surfaced three real bugs that compiling alone would never have caught:

1. **Enums weren't deserializing from JSON strings.** `POST /api/auth/dev-login` with `"role":"Claimant"` failed - System.Text.Json expects enums as integers by default. Fixed by registering `JsonStringEnumConverter` in `Program.cs`.
2. **DataAnnotations on record primary-constructor parameters threw at runtime in .NET 10**, not compile time: `[property: Required]` on a record parameter produced "validation metadata ... will be ignored" from the new ASP.NET Core 10 validation pipeline, which wants the attribute on the parameter, not the generated property. Fixed by dropping the `property:` target. This is a .NET 10-specific behavior change worth flagging if it surprises anyone else.
3. **The most significant one:** assigning a freshly-submitted claim always failed with a *false* "Concurrent update conflict" (409), on the very first assignment, with nobody else touching the row. Root cause: EF Core's default convention for Guid-typed keys is `ValueGeneratedOnAdd`. Because our domain code sets `Id = Guid.NewGuid()` itself (not the database), EF's heuristic for "is this a new entity or an already-persisted one reached via navigation fixup" misfired for the *new* `ClaimStatusHistory` row created inside `Claim.Assign()` - EF emitted an `UPDATE ... WHERE Id = @id` for a row that was never inserted, got zero rows affected, and (correctly, given its wrong premise) raised `DbUpdateConcurrencyException`. Fixed by explicitly configuring `.Property(x => x.Id).ValueGeneratedNever()` on every entity with an app-generated Guid key (User, Claim, ClaimStatusHistory, InformationRequest, ClaimDecision, NotificationLog) - the documented fix for client-generated keys in EF Core. Regenerated the `InitialCreate` migration after the fix.

**Human decision:** None required - these were implementation bugs caught and fixed within the same work session via real verification, not architectural judgment calls. Logged here per the brief's "do not claim functionality works without verification" principle - the first two attempts at the smoke test genuinely failed.

**Verification (final):** Full lifecycle verified via live HTTP calls against a real SQLite db: submit → assign → request-info → respond → approve (200s, correct status transitions, correct status history and decision recorded); re-approving an already-decided claim → 409; wrong role on a role-gated endpoint → 403; no token → 401; missing required field → 400 (DataAnnotations); motor claim missing vehicle registration → 400 (domain validation); claimant viewing another claimant's claim → 403 (ownership enforcement, independent of any frontend check). Kafka publisher correctly no-ops with a log line when `Kafka:Enabled=false` (the default - no broker running in this session yet).

**Next:** Automated xUnit tests (domain, application, API) + coverage (Phase 4).

---

## 2026-10-17 — Phase 4: Testing and quality

**AI actions:** Wrote 54 xUnit tests across Domain (state machine, every valid/invalid transition, reference generator), Application (use-case services via in-memory test doubles, including a simulated concurrent-assignment conflict), Infrastructure (Kafka event→topic mapping for all 5 event types with Kafka disabled), and Api (8 end-to-end HTTP tests via `WebApplicationFactory` against a real isolated-per-test SQLite file).

**Bugs this surfaced (fixed, not architectural):**
1. Two Application-layer tests asserted `Assert.Single(publisher.PublishedEvents)` but got 2-3 events — not a product bug, a test-setup bug: the test helpers created claims directly via `Claim.Submit()` (bypassing the real submission service) without clearing the resulting domain event, so later assertions double-counted events a real flow would already have dispatched-and-cleared in an earlier request. Fixed by clearing events in the test setup helpers.
2. **A genuine Domain design issue**, caught by `RequestInformation_WhenClaimNotYetAssigned_ThrowsInvalidClaimTransitionException` failing with the wrong exception type: `Claim.RequestInformation`, `RespondToInformationRequest`, and `Decide` all checked *ownership* before *transition validity*. For a claim still in `Submitted` status (nobody assigned yet), this meant the ownership check fired first and returned "not authorized" (`ClaimOwnershipException` → 403) instead of "wrong status for this action" (`InvalidClaimTransitionException` → 409) — a real, if minor, incorrect-HTTP-status bug a real officer could have hit. Fixed by reordering all three methods to check transition validity first, ownership second. Re-verified all existing passing tests still passed after the reorder.
3. HttpClient's JSON helpers in the test project don't inherit the server's `JsonStringEnumConverter` configuration — API tests failed to deserialize `role` until the test client was given its own matching `JsonSerializerOptions`.
4. `WebApplicationFactory` test cleanup threw `IOException` deleting the per-test SQLite file because SQLite can hold a pooled connection open briefly after host shutdown. Fixed with `SqliteConnection.ClearAllPools()` before delete, with a swallowed-exception fallback since it's non-critical temp-file cleanup.

**Human decision:** None required — all four were caught-and-fixed within this session's own verification loop, not judgment calls needing sign-off. Logged per "do not claim functionality works without verification" — item 2 in particular is the kind of bug that would have shipped unnoticed without writing the negative-case test.

**Verification (final):** `dotnet test ClaimsManagement.sln --collect:"XPlat Code Coverage"` → **54/54 passed, 0 failed, 0 skipped**. Coverage (via `reportgenerator`): **89.3% line / 68.6% branch / 92.6% method** overall; **Domain 89.5%, Application 90.7%** — both above the ~80% target on the layers carrying business rules. Full breakdown and known gaps (Kafka live-broker paths, a couple of unused repository query methods) documented honestly in `docs/TESTING.md`.

**Next:** Kafka producer/consumer live verification against the local native broker (Phase 5).

**Human decision:** User chose to push these 4 commits immediately (rather than batching with later phases) so GitHub Actions CI would run early, and to continue straight into Phase 5/6/7 without a review pause. Backend CI ran green on first push (`gh run list` confirmed `completed success`).

---

## 2026-10-17 — Phase 5: Kafka

**AI actions:** Wrote `scripts/kafka-start.ps1` (sets `JAVA_HOME` to the Temurin 21 JDK, since this machine's default `java` is 1.8 and Kafka 4.x needs 17+; generates a resolved `server.properties` from a committed template; runs `kafka-storage format --standalone` once, idempotently; starts the broker). Ran it for real against the pre-installed native Kafka 4.3.1 at `C:\kafka` - broker came up listening on `localhost:9092`.

Ran the API with `Kafka__Enabled=true` and drove the claim lifecycle via real HTTP calls.

**Bug found and fixed:** The first live run showed `ClaimSubmitted` events being produced and consumed correctly, but `ClaimAssigned`/`ClaimApproved` events sat unconsumed for over 20 seconds even though `kafka-topics --list` confirmed their topics existed (auto-created on first produce). Root cause: Confluent.Kafka's (librdkafka's) default topic-metadata refresh interval is ~5 minutes - fine for topics that exist before a consumer subscribes, too slow for topics that get auto-created seconds *after* subscription, which is exactly this app's situation (the consumer subscribes to all 5 topics at startup, before any of them necessarily exist). Fixed by setting `TopicMetadataRefreshIntervalMs = 5000` on the consumer config, documented inline as a pragmatic dev-scope fix (production would pre-create topics rather than rely on broker auto-create).

**Human decision:** None required - caught and fixed within this session's verification loop.

**Verification (final):** Re-ran the full lifecycle (submit → assign → request-info) with Kafka enabled. Confirmed via the API's own structured logs (`Published X ... to topic Y partition Z offset N` followed by `Recorded notification for X`) and by querying `claims.db` directly with `sqlite3`:

```
ClaimAssigned                   | 2B38EFD2-... | 2026-10-17 06:51:01
ClaimApproved                   | 2B38EFD2-... | 2026-10-17 06:51:01
ClaimSubmitted                  | A33133FB-... | 2026-10-17 06:51:24
ClaimAssigned                   | A33133FB-... | 2026-10-17 06:51:24
AdditionalInformationRequested  | A33133FB-... | 2026-10-17 06:51:28
```

Real rows, real event IDs, real timestamps - 4 of the 5 lifecycle event types verified live end-to-end (producer → broker → consumer → idempotent `NotificationLog` write). `ClaimRejected` wasn't separately re-verified live (my own test script tried to reject a claim that was still in `InfoRequested` status - correctly got a 409, confirming the state machine still enforces transitions correctly even under Kafka load); its topic-mapping is covered by `KafkaEventPublisherTests`, and it shares 100% of the code path with the already-verified `ClaimApproved` branch in `Claim.Decide()`. `docker-compose.yml` remains unexercised in this environment (no Docker installed) - the native-broker path above is what was actually run and verified.

**Next:** Minimal Angular frontend (Phase 6).

---

## 2026-10-17 — Phase 6: Angular frontend

**AI actions:** Scaffolded Angular in `claim-management-system-fe` via `npx @angular/cli@19` — the newest CLI major requires a newer Node than this machine has (22.18.0; needs 22.22.3+), so pinned to 19, the most recent compatible version (brief: "Angular, any version"). Moving the scaffold's output from `ng new`'s required subdirectory up to the repo root via `cp -r` proved extremely slow through Git Bash's POSIX emulation over `node_modules`' tens of thousands of files; killed that approach and used native PowerShell `Move-Item` (a same-volume rename, not a byte copy) instead - seconds instead of (projected) many minutes.

Built: `AuthService`/`ClaimsApiService` (typed HttpClient wrappers), a functional auth interceptor + route guard, and the claimant/officer feature components, sharing one `claim-detail` component that conditionally renders role-specific actions based on the logged-in user and the claim's current status - the answer to "how does the frontend serve two user types from one codebase" (see `claim-management-system-fe/CLAUDE.md`).

**Bugs found and fixed (via `ng build` failing, then via live browser testing):**
1. `@else if (claim(); as claim)` - Angular only allows the `as` binding on a primary `@if`, not `@else if` (`NG5002`). Restructured to `@else { @if (claim(); as claim) { ... } }`.
2. `FormBuilder` injected via constructor parameter but referenced in a field initializer (`readonly form = this.fb.group(...)`) in three components - field initializers run before the constructor body assigns constructor-parameter properties, so `this.fb` was `undefined` (`TS2729`). Fixed with `private readonly fb = inject(FormBuilder)` as a field instead.

**Verification - not just `ng build`:** Per the standing instruction to verify UI changes in a real browser, not just claim success from a build: started both the real backend (`dotnet run`, fresh SQLite db) and the real frontend (`ng serve`) locally, then drove the full golden path with Playwright against actual headless Chromium (no custom browser driver written - used the cached Chromium binary directly since `chromium-cli` wasn't installed in this environment). Script: claimant login → submit a Motor claim → officer login → assign from the unassigned queue → request additional information → claimant responds → officer approves with a settlement amount.

**Result:** 8/9 scripted assertions passed, zero browser console errors throughout. The 1 "failure" (a `.count()` check for the claimant's pending-info-request form reading 0 right after navigation) was a test-script timing artifact, not a product bug - confirmed by the very next screenshot (`08-claimant-respond-form.png`) showing the form fully rendered with the correct request text, and by the following step (filling and submitting that same form) succeeding. Final screenshot (`11-approved.png`) shows the complete, correct status history: Submitted → Assigned → InfoRequested → Assigned → Approved, with real timestamps.

**Human decision:** None required - implementation bugs caught and fixed within this session's own verification loop.

**Next:** Final verification scripts (`verify.ps1`/`verify.sh`) and documentation close-out (Phase 7).

---

## 2026-10-17 — Phase 7: Final verification

**AI actions:** Wrote `scripts/verify.ps1` (primary, matches this dev environment) and `scripts/verify.sh` (cross-platform companion) per the engineering brief's required checklist: environment prerequisites, restore, build, test, SQLite init, health check, submit/retrieve/assign over real REST, a valid transition, a rejected invalid transition, and an opt-in `-WithKafka`/`--with-kafka` broker-connectivity check that's off by default so ordinary verification never needs Kafka.

**Verification (final, both modes actually run, not assumed):**
```
./scripts/verify.ps1             -> 10/10 checks passed
./scripts/verify.ps1 -WithKafka  -> 11/11 checks passed (incl. live Kafka connectivity)
```
Both runs built Release, ran the full `dotnet test` suite, started a real API instance against a fresh temp SQLite database, and drove dev-login → submit → retrieve → assign → decide (valid) → re-decide (rejected, 409) over actual HTTP.

**Repository state at this point:**
- Backend (`claim-management-system-be`): 6 commits on `main`, all pushed, GitHub Actions CI green on every push so far.
- Frontend (`claim-management-system-fe`): 2 commits on `main`, pushed, GitHub Actions CI green.
- Local Kafka broker and both dev servers stopped and cleaned up (temp SQLite files removed) after verification — no leftover processes, no runtime databases committed.

**Human decision:** User chose to push commits eagerly (after the first backend milestone) rather than batch them, and to continue through all phases without a mid-session review pause — both choices held for the rest of the session. No other architectural decisions required sign-off during Phases 5-7; bugs found were fixed within the same verification loop and logged here, not escalated.

**Honest status at session end:** see README.md "Known limitations" (both repos) and `docs/ASSUMPTIONS.md` for the full deferred-scope list. Headline items not attempted: the operational dashboard (brief's "if time permits" item), a real transactional outbox for Kafka, any identity provider beyond the dev mock login.
