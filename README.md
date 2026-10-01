# OpportunityPilot — Web API

ASP.NET Core (.NET 10) API for OpportunityPilot. Clean Architecture:

```
src/
  OpportunityPilot.Domain          entities (Profile, JobApplication, AgentKey), ownership marker, enums
  OpportunityPilot.Application     services, DTOs, options, capability catalogue
  OpportunityPilot.Infrastructure  EF Core: SqlServer + Postgres contexts and migration sets
  OpportunityPilot.Api             controllers, auth (Supabase JWT / guest / dev bypass / agent key), hosting
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
| GET | `/api/v1/applications` | user | jobs the local agent acted on, newest first; `status`, `platform`, `take` (1–200, default 50), `skip` |
| GET | `/api/v1/applications/summary` | user | counts per status, applied in the last 7 days, last activity |
| POST | `/api/v1/applications/report` | agent key | the desktop agent reports up to 100 results; upserts by platform + job id, never downgrades `Applied` |
| GET | `/api/v1/agent-keys` | user | own active agent keys (prefix only, never the key) |
| POST | `/api/v1/agent-keys` | user | create a key (max 10 active); the plaintext key is returned once |
| DELETE | `/api/v1/agent-keys/{id}` | user | revoke (204, idempotent; 404 if not yours) |

The desktop agent sends its key in the `X-Agent-Key` header. Agent keys are
accepted only by `POST /api/v1/applications/report`; every other endpoint
requires a user sign-in, and the report endpoint rejects user tokens.

Errors are RFC 7807 problem details with a `correlationId`. OpenAPI is served
at `/openapi/v1.json` in Development.

## Tests

```bash
dotnet test     # integration tests need a running Docker daemon
```
