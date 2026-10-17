# claim-management-system-be

Backend for the Chubb APAC Fullstack Developer take-home assessment: an ASP.NET Core claims management API covering the motor/property claims lifecycle for claimants and claims officers. Frontend lives in the sibling repo [`claim-management-system-fe`](https://github.com/IvanLimChoonKiat/claim-management-system-fe).

> **Status:** under active development for the assessment (2–3h target / 5h hard cap). This README is updated as each phase completes — see `AI_WORKING_JOURNAL.md` for the real-time log and `docs/` for architecture/assumptions/testing detail.

## Architecture

Modular monolith, Clean Architecture layering: `Api → Application → Domain`, `Infrastructure → Application, Domain`. Domain has zero EF Core/HTTP/Kafka references. Full detail, domain model, and the REST-vs-Kafka rationale: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Explicit MVP boundaries and deferred scope: [`docs/ASSUMPTIONS.md`](docs/ASSUMPTIONS.md).

## Technology stack

- ASP.NET Core Web API, C#, **.NET 10**
- EF Core + **SQLite**
- Confluent.Kafka (local native KRaft broker — see "Running Kafka" below)
- xUnit + coverlet.collector
- GitHub Actions CI

## Setup

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet restore ClaimsManagement.sln
dotnet build ClaimsManagement.sln
```

## Database migrations

```bash
dotnet ef database update --project src/ClaimsManagement.Infrastructure --startup-project src/ClaimsManagement.Api
```

Creates/updates a local `claims.db` SQLite file (gitignored — never commit runtime databases). To add a new migration after a model change:

```bash
dotnet ef migrations add <Name> --project src/ClaimsManagement.Infrastructure --startup-project src/ClaimsManagement.Api
```

## Running the backend

```bash
dotnet run --project src/ClaimsManagement.Api
```

OpenAPI/Swagger UI available at the printed `https://localhost:xxxx/swagger` in `Development`.

## Running Kafka

**This development machine has no Docker installed.** Kafka is run against a pre-installed native Kafka 4.3.1 (KRaft-only) distribution instead:

```powershell
./scripts/kafka-start.ps1   # sets JAVA_HOME to a local JDK 21, formats storage on first run, starts the broker
```

A `docker-compose.yml` is included for portability to a machine with Docker, but **it has not been exercised in this environment** — only the native-binary path above was actually verified. See `docs/ARCHITECTURE.md` for delivery guarantees, retry/idempotency, and the Kafka-unavailable behavior.

## Unit & integration tests

```bash
dotnet test ClaimsManagement.sln
```

## Coverage

```bash
dotnet test ClaimsManagement.sln --collect:"XPlat Code Coverage"
```

Report lands under `TestResults/<run-id>/coverage.cobertura.xml`. Strategy and target: [`docs/TESTING.md`](docs/TESTING.md).

## Automated functional verification

```powershell
./scripts/verify.ps1              # build, test, SQLite init, REST lifecycle smoke test (no Kafka required)
./scripts/verify.ps1 -WithKafka   # also verifies the local Kafka broker produce/consume path
```

Cross-platform equivalent: `scripts/verify.sh`.

## GitHub Actions

`.github/workflows/ci.yml` — restores, builds Release, runs `dotnet test` with coverage collection, uploads test results and coverage as artifacts. Runs on push/PR to `main`/`develop` and manual dispatch. No Docker/Kafka dependency.

## Known limitations

See `docs/ASSUMPTIONS.md` for the full deferred-scope list and `docs/ARCHITECTURE.md`'s trade-offs table. Headline items: mock (non-production) authentication, SQLite instead of a server database, no transactional outbox for Kafka publishing, Kafka verified against a native local broker rather than Docker.
