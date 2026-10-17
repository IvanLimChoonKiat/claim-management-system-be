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
