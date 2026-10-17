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

**Next:** Domain model + claim state machine (Phase 3).
