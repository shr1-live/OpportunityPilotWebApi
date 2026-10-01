# OpportunityPilot — Web API

ASP.NET Core (.NET 10) API for OpportunityPilot. Clean Architecture:

```
src/
  OpportunityPilot.Domain          entities (Profile, JobApplication, AgentKey, Campaign, Source, ResearchJob,
                                   Evidence, Opportunity, Activity…), ownership marker, enums
  OpportunityPilot.Application     services, DTOs, options, capability catalogue, research runner and rules engine
  OpportunityPilot.Infrastructure  EF Core: SqlServer + Postgres contexts and migration sets; safe fetcher, HTML/feed parsing
  OpportunityPilot.Api             controllers, auth (Supabase JWT / guest / dev bypass / agent key), hosting, research processor
tests/
  OpportunityPilot.UnitTests         no I/O (rules, parsers, fetcher policy with stubbed network)
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
The research contract (entities, scoring, endpoints, DTOs) is **[docs/RESEARCH_CONTRACT.md](docs/RESEARCH_CONTRACT.md)**;
progress is tracked in [docs/IMPLEMENTATION_STATUS.md](docs/IMPLEMENTATION_STATUS.md) and decisions in [docs/DECISIONS.md](docs/DECISIONS.md).

## Endpoints (v1)

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/health/live` | none | process up |
| GET | `/health/ready` | none | database reachable (503 otherwise) |
| GET | `/api/v1/capabilities` | none | what this deployment can actually do; no secrets |
| GET | `/api/v1/overview` | user | counts for the Overview screen (profiles, applied, needs manual, campaigns, shortlisted) |
| GET | `/api/v1/profiles` | user | list own profiles |
| GET | `/api/v1/profiles/{id}` | user | one profile (404 if not yours) |
| POST | `/api/v1/profiles` | user | create (version 1) |
| PUT | `/api/v1/profiles/{id}` | user | update; `expectedVersion` required, 409 when stale |
| GET | `/api/v1/campaigns` | user | own campaigns, newest first, with source/opportunity counts and the last job |
| POST | `/api/v1/campaigns` | user | create a Job or Customer campaign (other modes: 400 "not supported yet"); weights normalised to 100 |
| GET | `/api/v1/campaigns/{id}` | user | one campaign with criteria and weights |
| PUT | `/api/v1/campaigns/{id}` | user | edit; `expectedVersion` required, 409 when stale |
| GET | `/api/v1/campaigns/{id}/sources` | user | the campaign's sources |
| POST | `/api/v1/campaigns/{id}/sources` | user | add a Paste (≤50 000 chars), Url or Feed source |
| DELETE | `/api/v1/campaigns/{id}/sources/{sourceId}` | user | remove a source and its rows (evidence already gathered is kept) |
| POST | `/api/v1/imports/preview` | user | CSV preview (≤1 MB, ≤1000 rows) with row-level errors; nothing imported yet |
| POST | `/api/v1/imports/{importId}/commit` | user | commit the valid rows of a preview into a Csv source (once, within 1 h) |
| POST | `/api/v1/campaigns/{id}/research` | user | queue a research run (202 `{ jobId }`; returns the active job if one is queued/running) |
| GET | `/api/v1/campaigns/{id}/research-jobs` | user | the campaign's last 20 jobs (no events) |
| GET | `/api/v1/research-jobs/{id}` | user | job state, stage, counts and the latest 100 events |
| POST | `/api/v1/research-jobs/{id}/cancel` | user | cancel (queued: at once; running: between sources, keeping results) |
| GET | `/api/v1/campaigns/{id}/opportunities` | user | scored opportunities; `outcome`, `status`, `sort=score\|recent`, `take` (≤200), `skip` |
| GET | `/api/v1/opportunities/{id}` | user | breakdown, facts, gaps, evidence and activity |
| PATCH | `/api/v1/opportunities/{id}/status` | user | move through the pipeline (records an activity) |
| GET | `/api/v1/campaigns/{id}/export` | user | spreadsheet-safe CSV (formula prefixes neutralised) |
| GET | `/api/v1/applications` | user | jobs the local agent acted on, newest first; `status`, `platform`, `take` (1–200, default 50), `skip` |
| GET | `/api/v1/applications/summary` | user | counts per status, applied in the last 7 days, last activity |
| POST | `/api/v1/applications/report` | agent key | the desktop agent reports up to 100 results; upserts by platform + job id, never downgrades `Applied`; an Applied item with `opportunityId` marks that opportunity Applied |
| GET | `/api/v1/agent/campaigns` | agent key | the owner's Job campaigns with their criteria |
| POST | `/api/v1/agent/campaigns/{id}/postings` | agent key | deliver up to 100 LinkedIn/Naukri postings into the campaign's Agent source; optionally queue research |
| GET | `/api/v1/agent/shortlist` | agent key | shortlisted Job opportunities not yet applied to; `platform` |
| GET | `/api/v1/agent-keys` | user | own active agent keys (prefix only, never the key) |
| POST | `/api/v1/agent-keys` | user | create a key (max 10 active); the plaintext key is returned once |
| DELETE | `/api/v1/agent-keys/{id}` | user | revoke (204, idempotent; 404 if not yours) |

The desktop agent sends its key in the `X-Agent-Key` header. Agent keys are
accepted only by `POST /api/v1/applications/report` and `/api/v1/agent/*`;
every other endpoint requires a user sign-in, and the agent endpoints reject
user tokens.

Research runs in a single in-process background worker (`ResearchProcessor`)
that claims durable jobs with a lease. It only runs while the API runs; on a
free host that sleeps, queued jobs wait until the next request wakes it.

Errors are RFC 7807 problem details with a `correlationId`. OpenAPI is served
at `/openapi/v1.json` in Development.

## Tests

```bash
dotnet test     # integration tests need a running Docker daemon
```
