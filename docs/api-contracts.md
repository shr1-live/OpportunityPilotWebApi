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

Supabase access tokens must have the configured issuer and audience, a future expiry, a GUID `sub`, and a valid
`RS256`, `ES256` or `EdDSA` signature from the project's JWKS. `HS256` is accepted only when the server-only
`Auth__LegacyJwtSecret` is explicitly configured; that symmetric key is never offered to asymmetric tokens. Guest
credentials are separate opaque `opg_…` bearer tokens; only SHA-256 hashes are persisted and their scheme never
accepts a Supabase JWT.

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

### Overview, analytics and profiles

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/v1/overview` | user | — | 200 `{ profiles, applied, needsManual, campaigns, shortlisted }` |
| GET | `/api/v1/analytics/overview` | user | query `workspace=Candidate|Sales` (required), `days` (1–365, default 30) | 200 `AnalyticsOverviewDto`; 400 field errors |
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
| POST | `/api/v1/campaigns/{campaignId}/sources` | user | `CreateSourceRequest` (Paste, Url, Feed, Greenhouse, Lever, Adzuna, Ashby, SmartRecruiters, Recruitee, Workable, Remotive or RemoteOk); body ≤256 KB | 201 `SourceDto`; 400; 404; 413 |
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

### Cover-note drafts

Only deterministic `CoverNote` generation is implemented in this slice. It requires a Job opportunity whose campaign
profile is confirmed. No message is sent; approval only makes the exact approved text available to the desktop agent.

| Method | Path | Auth | Request | Response / errors |
|---|---|---|---|---|
| POST | `/api/v1/opportunities/{id}/drafts` | user | `{ channel: Email|CoverNote|LinkedInMessage|ContactForm, recipient? }` | 201 `DraftDto`; confirmed profile required; CoverNote is Job-only; 404/409 |
| GET | `/api/v1/opportunities/{id}/drafts` | user | — | 200 `DraftDto[]`, newest first; 404 |
| GET | `/api/v1/drafts/{id}` | user | — | 200 `DraftDto`; 404 |
| PUT | `/api/v1/drafts/{id}` | user | `{ recipient?, subject?, body, expectedVersion }` | 200 `DraftDto`; material edits increment version and clear approval; 400; 404; 409 |
| POST | `/api/v1/drafts/{id}/approve` | user | `{ version }` | 200 `DraftDto`; approval is bound to SHA-256 of exact content and version; 400; 404; 409 |
| POST | `/api/v1/drafts/{id}/revoke-approval` | user | — | 200 `DraftDto`; 404; 409 |
| GET | `/api/v1/drafts` | user | query `state?`, `take`, `skip` | cross-opportunity `DraftPageDto` |
| POST | `/api/v1/drafts/batch-approve` | user | `{ items: [{ id, version }] }` | independent exact-version results; blocked/stale items are skipped with reasons |
| GET/POST/DELETE | `/api/v1/suppressions[/{id}]` | user | recipient/reason on POST | owner suppression list |
| GET/POST | `/api/v1/opportunities/{id}/activities` | user | `kind, detail, occurredAt?` on POST | activity history; observed stages update status |
| GET/POST | `/api/v1/opportunities/{id}/next-actions` | user | next-action fields on POST | opportunity reminders |
| GET/PATCH | `/api/v1/next-actions[/{id}]` | user | `state?`, `dueAt?` on PATCH | owner due-date list and state changes |
| POST | `/api/v1/goal-previews` | user | `{ profileId, goal, mode? }` | Gemini when configured, validated deterministic fallback otherwise |
| GET | `/api/v1/ai/status` | user | — | provider/model/fallback status |
| GET/DELETE | `/api/v1/account-data/export`, `/api/v1/account-data` | user | `{ confirm: true }` on DELETE | portable JSON export or permanent owned-data deletion |
| DELETE | `/api/v1/drafts/{id}` | user | — | 204; 404 |

### Approval queue

| Method | Path | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/v1/approvals` | user | query `campaignId?`, `take` (1–200, default 100), `skip` | 200 `{ total, items: ApprovalItem[] }`, Suggested opportunities by score |
| POST | `/api/v1/approvals/decide` | user | `{ approve?: Guid[], reject?: Guid[] }`, 1–200 distinct ids, no overlap | 200 `{ approved, rejected, skipped }`; approve → Shortlisted, reject → Dismissed; stale/foreign ids are skipped; 400 |

### Sales pipeline (N5 first slice)

The complete sales design is documented in [SALES_CONTRACT.md](SALES_CONTRACT.md). This first slice supports manually
entered projects and versioned bid preparation/approval. Provider discovery, bid placement, tenders and sales drafts
remain unavailable.

| Method | Path | Auth | Request | Response / errors |
|---|---|---|---|---|
| GET | `/api/v1/sales/projects` | user | query `state?`, `source?`, `take` (1–200, default 50), `skip` | 200 `SalesProjectDto[]`, newest updated first |
| POST | `/api/v1/sales/projects` | user | `CreateSalesProjectRequest`; non-manual sources require `externalId` | 201 `SalesProjectDto`; 400; 409 duplicate provider id |
| GET | `/api/v1/sales/projects/{id}` | user | — | 200 `SalesProjectDto`; 404 |
| POST | `/api/v1/sales/bids/batch-approve` | user | `{ items: [{ id, version }] }` | exact-version per-item results |
| POST | `/api/v1/sales/projects/{id}/handoff` | user | — | marks manual tender/provider handoff |
| POST | `/api/v1/sales/projects/{id}/bid` | user | `CreateSalesBidRequest` | 200 project with the new Draft bid; 400; 404 |
| PUT | `/api/v1/sales/bids/{id}` | user | `UpdateSalesBidRequest` with `expectedVersion` | 200 project; 400; 404; 409 stale version |
| POST | `/api/v1/sales/bids/{id}/approve` | user | `{ version }` | 200 project; 404; 409 stale version |

## DTO shapes

JSON is camelCase and enums are strings. `?` marks nullable.

| DTO | Fields |
|---|---|
| CapabilitiesDto | `environment, databaseProvider, aiMode, setupRequired: string[], guestSignIn: bool, temporaryStorage: bool, items: CapabilityDto[]` |
| CapabilityDto | `key, name, category, status (Ready, Configured, NotConfigured, Disabled, ManualHandoff, NotBuilt, LocalAgent), detail, can: string[], cannot: string[]` |
| AnalyticsOverviewDto | `workspace, days, generatedAt, campaignCount, kpis, funnel, fitHistogram, unknownCriteria, sources, applicationsPerDay?, attention, activeResearch?, qualifiedByIndustry?, signalsFound?`; fields that stored data cannot answer are `null`, never estimates (full semantics in `ANALYTICS_CONTRACT.md`) |
| ProfileSummaryDto | `id, type, name, version, confirmedAt?, updatedAt` |
| ProfileDto | `id, type, name, data (JSON object), version, confirmedAt?, createdAt, updatedAt` |
| CreateProfileRequest | `type (Product, Business, Candidate, Services), name, data?, confirmed` |
| UpdateProfileRequest | `name, data?, confirmed, expectedVersion` |
| CampaignSummaryDto | `id, profileId, mode, name, goal, resultLimit, version, createdAt, updatedAt, sourceCount, opportunityCount, lastJob? { id, state, stage, finishedAt? }, autoSuggestMinScore?` |
| CampaignDto | CampaignSummaryDto + `criteria: CampaignCriteria, weights: { [criterion]: int }` |
| CampaignCriteria | `keywords, requiredSkills, preferredSkills, locations, workModes, industries, problems, signals, excludeKeywords, excludeOrganizations` (all `string[]`), `candidateYears?: number` |
| CreateCampaignRequest | `profileId, mode, name, goal?, criteria?, weights? { [criterion]: number }, resultLimit?, autoSuggestMinScore?` |
| UpdateCampaignRequest | `name, goal?, criteria?, weights?, resultLimit?, expectedVersion, autoSuggestMinScore?` (omitted values keep stored ones; explicit null disables auto-suggest) |
| CreateSourceRequest | `kind, label?, url?, text?, permissionNote?`; per-company boards require a safe slug/board URL, aggregate Remotive/RemoteOk and Adzuna take no URL |
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
| AgentShortlistItem | `opportunityId, campaignId, platform, externalId, url, title, organization, coverNote?`; `coverNote` is non-null only for a currently hash-valid approved CoverNote |
| DraftDto | `id, opportunityId, channel, recipient?, recipientVerified, subject?, body, version, state, approvedVersion?, approvedAt?, source, fallbackReason?, claims[], sendReady, sendBlockers[], createdAt, updatedAt` |
| ApprovalItem | `opportunityId, campaignId, campaignName, title, organization, location?, platform?, applyUrl?, score, coverage, outcomeReason?, appliesVia (Agent, You)` |
| SalesProjectDto | `id, source, externalId?, title, buyer?, description?, url?, deadlineUtc?, state, version, bids[], createdAt, updatedAt` |
| SalesBidDto | `id, projectId, amount, currency, deliveryDays, proposal, version, state, approvedVersion?, approvedAt?, hasValidApproval, createdAt, updatedAt` |
| CreateSalesProjectRequest | `source (Manual in this slice), externalId?, title, buyer?, description?, url?, deadlineUtc?, evidenceJson?` |
| CreateSalesBidRequest | `amount, currency, deliveryDays, proposal` |
| UpdateSalesBidRequest | `amount, currency, deliveryDays, proposal, expectedVersion` |

## Planned, not built

Nothing below exists in the code; do not call it or document it as available.

| Source | Endpoints |
|---|---|
| [M4_M5_CONTRACT.md](M4_M5_CONTRACT.md) | opportunity AI summary and persisted Gemini usage remain; goal previews, drafts, suppressions, activities, next actions and AI status are built |
| Implementation plan §16 | `POST /api/v1/research-jobs/{id}/resume`, AI exchanges, Gmail integration, sending |
