# API contracts

**Authoritative list of what the API exposes today**, taken from `src/OpportunityPilot.Api/Controllers/*.cs`.
Slice specs ([RESEARCH_CONTRACT.md](RESEARCH_CONTRACT.md), [M4_M5_CONTRACT.md](M4_M5_CONTRACT.md)) describe designs;
when they disagree with this file, this file (and the code) wins. Update it in the same change as any endpoint.

Envelope, naming and error rules: [conventions.md](conventions.md). Limits and rules behind each endpoint:
[business-rules.md](business-rules.md). States: [opportunity-lifecycle.md](opportunity-lifecycle.md).

## Authentication modes

| Auth | How the caller proves it | Where accepted |
|---|---|---|
| anonymous | nothing | only endpoints marked `[AllowAnonymous]` |
| user | `Authorization: Bearer <Supabase JWT>`; in demo mode a guest token from `POST /api/v1/auth/guest`; in Development with `Auth:DevBypass` the header `X-Dev-User: <any name>` | every endpoint without its own scheme (the fallback policy) |
| agent key | `X-Agent-Key: opk_…` | only endpoints marked `[Authorize(AuthenticationSchemes = "AgentKey")]`; user credentials are rejected there (401) and agent keys are rejected everywhere else (401) |

Owner identity is always the token's `sub`. Another owner's id answers **404**, never 403.

## Endpoints

Status codes listed are the ones the code can produce besides 401 (missing or wrong credentials) and 429 (rate limit).
Query enums that fail to bind return 400 from model binding.

### Health, capabilities, auth

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/health/live` | anonymous | — | 200 `{ status: "live" }` |
| GET | `/health/ready` | anonymous | — | 200 `{ status: "ready", checks: { database: "Healthy" } }`; 503 with `status: "not-ready"` |
| GET | `/api/v1/capabilities` | anonymous | — | 200 `CapabilitiesDto` (no secrets) |
| POST | `/api/v1/auth/guest` | anonymous | — | 200 `{ token, expiresAt }`; 404 when guest sign-in is off (Supabase configured or dev bypass on) |
| GET | `/openapi/v1.json` | anonymous | — | OpenAPI document, **Development only** |

### Overview and profiles

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/v1/overview` | user | — | 200 `{ profiles, applied, needsManual, campaigns, shortlisted }` |
| GET | `/api/v1/profiles` | user | — | 200 `ProfileSummaryDto[]`, latest update first |
| GET | `/api/v1/profiles/{id}` | user | — | 200 `ProfileDto`; 404 |
| POST | `/api/v1/profiles` | user | `CreateProfileRequest` | 201 `ProfileDto` + `Location`; 400 |
| PUT | `/api/v1/profiles/{id}` | user | `UpdateProfileRequest` | 200 `ProfileDto`; 400; 404; 409 stale `expectedVersion` |

### Campaigns, sources, imports

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/v1/campaigns` | user | — | 200 `CampaignSummaryDto[]`, newest first |
| GET | `/api/v1/campaigns/{id}` | user | — | 200 `CampaignDto`; 404 |
| POST | `/api/v1/campaigns` | user | `CreateCampaignRequest` | 201 `CampaignDto` + `Location`; 400 (also for Partner/Investor/Freelance: "not supported yet", and for a profile that is not the caller's) |
| PUT | `/api/v1/campaigns/{id}` | user | `UpdateCampaignRequest` | 200 `CampaignDto`; 400 (validated before the version check); 404; 409 |
| GET | `/api/v1/campaigns/{campaignId}/sources` | user | — | 200 `SourceDto[]`, oldest first; 404 |
| POST | `/api/v1/campaigns/{campaignId}/sources` | user | `CreateSourceRequest` (Paste, Url or Feed only); body ≤256 KB | 201 `SourceDto`; 400; 404; 413 |
| DELETE | `/api/v1/campaigns/{campaignId}/sources/{sourceId}` | user | — | 204; 404 |
| POST | `/api/v1/imports/preview` | user | `{ campaignId, csv }`; body ≤3 MB | 200 `ImportPreviewDto`; 400; 404; 413 |
| POST | `/api/v1/imports/{importId}/commit` | user | optional `{ label }` | 201 `SourceDto` (Kind Csv); 400 (no valid rows, label too long, 20-source limit); 404 (unknown or expired); 409 (already committed) |

### Research and opportunities

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| POST | `/api/v1/campaigns/{campaignId}/research` | user | — | 202 `{ jobId }` (the existing job when one is Queued/Running); 400 (no sources); 404; 409 (rare race) |
| GET | `/api/v1/campaigns/{campaignId}/research-jobs` | user | — | 200 `ResearchJobDto[]`, newest first, max 20, `events` omitted; 404 |
| GET | `/api/v1/research-jobs/{id}` | user | — | 200 `ResearchJobDto` with the latest 100 `events`, newest first; 404 |
| POST | `/api/v1/research-jobs/{id}/cancel` | user | — | 202 `ResearchJobDto`; 404; 409 (job changing, retry) |
| GET | `/api/v1/campaigns/{campaignId}/opportunities` | user | query `outcome?`, `status?`, `sort=score` (default) or `recent`, `take` (clamped 1–200, default 50), `skip` | 200 `{ total, items: OpportunitySummaryDto[] }`; 404 |
| GET | `/api/v1/opportunities/{id}` | user | — | 200 `OpportunityDetailDto`; 404 |
| PATCH | `/api/v1/opportunities/{id}/status` | user | `{ status }` | 200 `OpportunityDetailDto`; 400; 404; 409 (changed concurrently, e.g. by research) |
| GET | `/api/v1/campaigns/{campaignId}/export` | user | — | 200 `text/csv; charset=utf-8` with BOM, file `<campaign-slug>-opportunities.csv`, ≤5000 rows by score; 404 |

### Applications and agent keys

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/v1/applications` | user | query `status?`, `platform?`, `take` (clamped 1–200, default 50), `skip` | 200 `{ total, items: ApplicationDto[] }`, newest `occurredAt` first |
| GET | `/api/v1/applications/summary` | user | — | 200 `ApplicationSummaryDto` |
| POST | `/api/v1/applications/report` | **agent key** | `ApplicationReportRequest` (1–100 items) | 200 `{ accepted }`; 400 field errors; 409 concurrent report |
| GET | `/api/v1/agent-keys` | user | — | 200 `AgentKeyDto[]` (active keys, newest first, prefix only) |
| POST | `/api/v1/agent-keys` | user | `{ name }` | 201 `CreatedAgentKeyDto` (the only time `key` is returned); 400 (name, 10 active keys) |
| DELETE | `/api/v1/agent-keys/{id}` | user | — | 204 (idempotent); 404 |

### Desktop agent research

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/v1/agent/campaigns` | **agent key** | — | 200 `AgentCampaignDto[]`: the key owner's **Job** campaigns, newest first |
| POST | `/api/v1/agent/campaigns/{id}/postings` | **agent key** | `AgentPostingsRequest`; body ≤4 MB | 200 `{ accepted, sourceId, jobId }` (`jobId` null unless `queueResearch`); 400 (not a Job campaign, field errors); 404; 409; 413 |
| GET | `/api/v1/agent/shortlist` | **agent key** | query `platform?` (LinkedIn, Naukri, Other) | 200 `AgentShortlistItem[]`, best score first, max 200 |

## DTO shapes

JSON is camelCase and enums are strings. `?` marks nullable.

| DTO | Fields |
|---|---|
| CapabilitiesDto | `environment, databaseProvider, aiMode, setupRequired: string[], guestSignIn: bool, temporaryStorage: bool, items: CapabilityDto[]` |
| CapabilityDto | `key, name, category, status (Ready, Configured, NotConfigured, Disabled, ManualHandoff, NotBuilt, LocalAgent), detail, can: string[], cannot: string[]` |
| ProfileSummaryDto | `id, type, name, version, confirmedAt?, updatedAt` |
| ProfileDto | `id, type, name, data (JSON object), version, confirmedAt?, createdAt, updatedAt` |
| CreateProfileRequest | `type (Product, Business, Candidate, Services), name, data?, confirmed` |
| UpdateProfileRequest | `name, data?, confirmed, expectedVersion` |
| CampaignSummaryDto | `id, profileId, mode, name, goal, resultLimit, version, createdAt, updatedAt, sourceCount, opportunityCount, lastJob? { id, state, stage, finishedAt? }` |
| CampaignDto | CampaignSummaryDto + `criteria: CampaignCriteria, weights: { [criterion]: int }` |
| CampaignCriteria | `keywords, requiredSkills, preferredSkills, locations, workModes, industries, problems, signals, excludeKeywords, excludeOrganizations` (all `string[]`), `candidateYears?: number` |
| CreateCampaignRequest | `profileId, mode, name, goal?, criteria?, weights? { [criterion]: number }, resultLimit?` |
| UpdateCampaignRequest | `name, goal?, criteria?, weights?, resultLimit?, expectedVersion` (omitted weights keep the stored ones) |
| CreateSourceRequest | `kind (Paste, Url, Feed), label?, url?, text?, permissionNote?` |
| SourceDto | `id, campaignId, kind, label, url?, platform?, permissionNote?, status, lastFetchedAt?, safeError?, itemCount, textLength, createdAt` |
| ImportPreviewDto | `importId, columns: string[], warnings: string[], rows: { row, values: { [column]: string }, errors: string[] }[] (first 50), validCount, errorCount` |
| ResearchJobDto | `id, campaignId, state, stage, createdAt, startedAt?, finishedAt?, safeError?, counts: { sources, sourcesDone, sourcesFailed, fetched, candidates, qualified, needsVerification, excluded }, events?: { at, stage, level, message }[]` |
| OpportunitySummaryDto | `id, campaignId, mode, title, organization, location?, url?, applyUrl?, platform?, score, coverage, outcome, outcomeReason?, status, gapsCount, updatedAt` |
| OpportunityDetailDto | OpportunitySummaryDto + `description?, version, breakdown: { criterion, label, weight, value?: 1/0.5/0, points, reason, evidenceIds }[], facts: { key, label, value, evidenceId?, isInference }[], gaps: string[], evidence: { id, sourceId, sourceLabel?, url?, retrievedAt, excerpt, extractionMethod }[], activities: { kind, occurredAt, detail }[] (latest 100)` |
| ApplicationReportRequest | `items: { platform (LinkedIn, Naukri, Instahyre), externalJobId, jobUrl, title, company, location?, status (Applied, DryRun, NeedsManual, Skipped, Failed), detail?, occurredAt, opportunityId? }[]` |
| ApplicationDto | `id, platform, externalJobId, jobUrl, title, company, location?, status, detail?, occurredAt, updatedAt` |
| ApplicationSummaryDto | `applied, appliedLast7Days, needsManual, dryRun, skipped, failed, lastActivityAt?` |
| AgentKeyDto | `id, name, prefix, createdAt, lastUsedAt?` (CreatedAgentKeyDto adds `key`) |
| AgentCampaignDto | `id, name, mode, criteria: CampaignCriteria` |
| AgentPostingsRequest | `platform (LinkedIn or Naukri), items: { externalId, url, title, company, location?, description? }[] (1–100), queueResearch: bool` |
| AgentShortlistItem | `opportunityId, campaignId, platform, externalId, url, title, organization` |

## Planned, not built

Nothing below exists in the code; do not call it or document it as available.

| Source | Endpoints |
|---|---|
| [M4_M5_CONTRACT.md](M4_M5_CONTRACT.md) | goal previews, opportunity AI summary, drafts, suppressions, activities, next actions, `/api/v1/ai/status` |
| Implementation plan §16 | `POST /api/v1/research-jobs/{id}/resume`, AI exchanges, Gmail integration, sending |
