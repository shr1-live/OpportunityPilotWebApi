# OpportunityPilot — Web API

ASP.NET Core (.NET 10) API for OpportunityPilot. Clean Architecture:

```
src/
  OpportunityPilot.Domain          entities (Profile), ownership marker, enums
  OpportunityPilot.Application     services, DTOs, options, capability catalogue
  OpportunityPilot.Infrastructure  EF Core: SqlServer + Postgres contexts and migration sets
  OpportunityPilot.Api             controllers, auth (Supabase JWT / dev bypass), hosting
tests/
  OpportunityPilot.UnitTests         no I/O
  OpportunityPilot.IntegrationTests  real API + Postgres via Testcontainers (needs Docker)
```

## Quick start (local)

```bash
dotnet run --project src/OpportunityPilot.Api     # http://localhost:5051
```

Development uses SQL Server LocalDB, migrates on startup, and accepts the
`X-Dev-User` header instead of a Supabase token. Pair it with the web app's
`VITE_DEV_AUTH=true`.

Full setup, configuration reference, migrations and deployment: **[docs/SETUP.md](docs/SETUP.md)**.

## Endpoints (v1)

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/health/live` | none | process up |
| GET | `/health/ready` | none | database reachable (503 otherwise) |
| GET | `/api/v1/capabilities` | none | what this deployment can actually do; no secrets |
| GET | `/api/v1/overview` | user | counts for the Overview screen |
| GET | `/api/v1/profiles` | user | list own profiles |
| GET | `/api/v1/profiles/{id}` | user | one profile (404 if not yours) |
| POST | `/api/v1/profiles` | user | create (version 1) |
| PUT | `/api/v1/profiles/{id}` | user | update; `expectedVersion` required, 409 when stale |

Errors are RFC 7807 problem details with a `correlationId`. OpenAPI is served
at `/openapi/v1.json` in Development.

## Tests

```bash
dotnet test     # integration tests need a running Docker daemon
```
