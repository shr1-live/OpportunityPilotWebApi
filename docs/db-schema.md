# Database schema

Source of truth: `src/OpportunityPilot.Infrastructure/Persistence/AppDbContext.cs` (shared model) and
`ProviderContexts.cs` (provider differences). This file must change in the same commit as any entity, mapping or
migration change. How to add a migration: [skills/migrations.SKILL.md](../skills/migrations.SKILL.md).

## Conventions that apply to every table

| Rule | Detail |
|---|---|
| Schema | Everything lives in schema `app`, including `__EFMigrationsHistory` (Supabase's Data API does not expose it by default) |
| Table names | snake_case plural, set explicitly with `ToTable` |
| Column names | EF default: the C# property name (PascalCase), e.g. `OwnerId` |
| Primary keys | `Id` GUID, generated in the domain constructor (`ValueGeneratedNever`), except `opportunity_evidence` (composite) |
| Ownership | Every user-owned table has `OwnerId` (GUID from the token's `sub`). There is no FK to a users table; Supabase owns identities |
| Enums | Stored as strings (`HasConversion<string>()`), max 32 characters. Renaming an enum member breaks stored rows |
| Times | UTC everywhere. SqlServer `datetime2` is read back with `DateTimeKind.Utc` (`UtcDateTimeConverter`); Postgres `timestamp with time zone` |
| JSON columns | `jsonb` on Postgres, `nvarchar(max)` on SqlServer, text in InMemory. Never empty: default `{}` or `[]` |
| Concurrency | Optimistic: a property marked `IsConcurrencyToken` (no rowversion). The domain increments it on each change |
| Index names | EF default `IX_<table>_<columns>`; the one hand-named index is `UX_research_jobs_active_campaign` |

Type notation below: `uuid`, `string(n)` (Postgres `character varying(n)` / SqlServer `nvarchar(n)`, or `nvarchar(max)` when n > 4000),
`int`, `bool`, `time` (UTC timestamp), `json`. "null" marks a nullable column; everything else is NOT NULL.

## Tables

### profiles — `Domain/Profiles/Profile.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| Type | string(32) | `ProfileType`: Product, Business, Candidate, Services |
| Name | string(200) | trimmed |
| StructuredDataJson | json | any JSON object, ≤32 KB (checked in `ProfileService`) |
| Version | int | concurrency token; 1 after create, +1 per update |
| ConfirmedAt | time, null | set to the save time when the request says `confirmed: true`, cleared otherwise |
| CreatedAt, UpdatedAt | time | |

Indexes: `(OwnerId, UpdatedAt)`.

### job_applications — `Domain/Applications/JobApplication.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| Platform | string(32) | `ApplicationPlatform`: LinkedIn, Naukri, Instahyre |
| ExternalJobId | string(100) | the platform's job id, trimmed |
| JobUrl | string(1000) | |
| Title, Company | string(300) | |
| Location | string(200), null | |
| Status | string(32) | `ApplicationStatus`: Applied, DryRun, NeedsManual, Skipped, Failed |
| Detail | string(1000), null | |
| OccurredAt | time | when the agent acted |
| CreatedAt, UpdatedAt | time | |

Indexes: unique `(OwnerId, Platform, ExternalJobId)`; `(OwnerId, OccurredAt)`. No concurrency token.

### agent_keys — `Domain/Agents/AgentKey.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| Name | string(100) | |
| KeyHash | string(64) | lowercase hex SHA-256 of the key; the key itself is never stored |
| Prefix | string(12) | first 12 characters of the key, for display |
| CreatedAt | time | |
| LastUsedAt, RevokedAt | time, null | revocation keeps the first time |

Indexes: unique `KeyHash`; `OwnerId`.

### campaigns — `Domain/Campaigns/Campaign.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| ProfileId | uuid | FK → profiles, **Restrict** |
| Mode | string(32) | `OpportunityMode`; fixed at creation |
| Name | string(200) | |
| Goal | string(2000) | empty string when not given |
| CriteriaJson | json | `CampaignCriteria`, camelCase |
| WeightsJson | json | `{ "<criterionKey>": int }`, sums to 100 |
| ResultLimit | int | 1–100, default 25 |
| AutoSuggestMinScore | int, null | Job-only approval threshold, 1–100; null disables auto-suggest |
| Version | int | concurrency token |
| CreatedAt, UpdatedAt | time | |

Indexes: `(OwnerId, CreatedAt)`; `ProfileId`.

### sources — `Domain/Research/Source.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| CampaignId | uuid | FK → campaigns, Cascade |
| Kind | string(32) | `SourceKind`: Paste, Csv, Url, Feed, Agent, Greenhouse, Lever, Adzuna, Ashby, SmartRecruiters, Recruitee, Workable, Remotive, RemoteOk |
| Label | string(200) | |
| Url | string(1000), null | Url and Feed only |
| Text | string(50000), null | Paste only (SqlServer `nvarchar(max)`) |
| PermissionNote | string(500), null | |
| Platform | string(32), null | `JobPlatform`; set on Agent source rows; fetched board candidates carry their platform into opportunities |
| Status | string(32) | `SourceStatus`: Pending, Ok, Failed, Skipped |
| LastFetchedAt | time, null | |
| SafeError | string(500), null | user-facing reason; cleared on Ok |
| ItemCount | int | rows (Csv/Agent), parsed postings (Paste) or entries found (Url/Feed) |
| CreatedAt | time | |

Indexes: `(OwnerId, CampaignId, CreatedAt)`; `CampaignId`.

### source_items — `Domain/Research/SourceItem.cs`

Rows of Csv and Agent sources.

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| SourceId | uuid | FK → sources, Cascade |
| ExternalId | string(100), null | platform job id (Agent) or CSV `id` column |
| Title | string(300) | job title, or company name for Customer rows |
| Organization | string(300) | empty string when unknown |
| Location | string(200), null | |
| Url, Website | string(1000), null | |
| Description | string(20000), null | SqlServer `nvarchar(max)` |
| Country | string(100), null | |
| Industry | string(200), null | |
| CreatedAt, UpdatedAt | time | |

Indexes: unique **filtered** `(SourceId, ExternalId) WHERE ExternalId IS NOT NULL`; `(SourceId, UpdatedAt)`.

### import_batches — `Domain/Research/ImportBatch.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| CampaignId | uuid | FK → campaigns, Cascade |
| RowsJson | json | `ImportRowDto[]` (every row with its errors), camelCase |
| CreatedAt, ExpiresAt | time | expires 1 hour after creation |
| Committed | bool | **concurrency token**: two simultaneous commits cannot both succeed |

Indexes: `(OwnerId, CreatedAt)`; `CampaignId`.

### research_jobs — `Domain/Research/ResearchJob.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| CampaignId | uuid | FK → campaigns, Cascade |
| State | string(32) | `ResearchJobState` |
| Stage | string(32) | `ResearchStage` |
| CountsJson | json | `ResearchCounts`, camelCase |
| Attempts | int | number of claims |
| LeaseUntil | time, null | |
| CancelRequested | bool | |
| CreatedAt | time | |
| StartedAt, FinishedAt | time, null | |
| SafeError | string(500), null | |
| Version | int | concurrency token; starts at 1 |

Indexes: `(OwnerId, CampaignId, CreatedAt)`; `(State, CreatedAt)` (processor poll);
unique **filtered** `UX_research_jobs_active_campaign` on `CampaignId WHERE State IN ('Queued','Running')`.

### research_events — `Domain/Research/ResearchEvent.cs`

Not owner-stamped; read only through its job.

| Column | Type | Notes |
|---|---|---|
| Id | uuid | |
| JobId | uuid | FK → research_jobs, Cascade |
| At | time | strictly increasing within a run |
| Stage | string(32) | |
| Level | string(32) | `EventLevel`: Info, Warning, Error |
| Message | string(500) | safe summary, truncated with "…" |

Indexes: `(JobId, At)`.

### evidence — `Domain/Research/Evidence.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| CampaignId | uuid | FK → campaigns, Cascade |
| SourceId | uuid | **no FK on purpose**: deleting a source keeps the evidence behind existing scores |
| Url | string(1000), null | |
| RetrievedAt | time | |
| ContentHash | string(64) | lowercase hex SHA-256 of the stored excerpt |
| Excerpt | string(2000) | |
| ExtractionMethod | string(32) | currently always `Rules` |

Indexes: `(CampaignId, SourceId, ContentHash)` (not unique); `OwnerId`.

### opportunities — `Domain/Opportunities/Opportunity.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| CampaignId | uuid | FK → campaigns, Cascade |
| Mode | string(32) | copied from the campaign |
| DedupeKey | string(400) | see [business-rules.md](business-rules.md#dedupe-and-upsert) |
| Title, Organization | string(300) | Organization is empty when unknown |
| Location | string(200), null | |
| Url, ApplyUrl | string(1000), null | a URL longer than 1000 is dropped, not cut |
| Platform | string(32), null | `JobPlatform` |
| ExternalId | string(100), null | |
| Description | string(4000), null | |
| Score, Coverage | int | 0–100 |
| Outcome | string(32) | `FilterOutcome` |
| OutcomeReason | string(500), null | |
| Status | string(32) | `OpportunityStatus` |
| BreakdownJson | json | `BreakdownRow[]` |
| FactsJson | json | `FactRow[]` |
| GapsJson | json | `string[]` |
| GapsCount | int | denormalised from GapsJson for list queries |
| LastResearchJobId | uuid, null | no FK |
| CreatedAt, UpdatedAt | time | |
| Version | int | concurrency token |

Indexes: unique `(CampaignId, DedupeKey)`; `(OwnerId, CampaignId, Score)`; `(OwnerId, Status, UpdatedAt)`.

### opportunity_evidence — `Domain/Opportunities/Activity.cs` (`OpportunityEvidence`)

| Column | Type | Notes |
|---|---|---|
| OpportunityId | uuid | PK part; FK → opportunities, Cascade |
| EvidenceId | uuid | PK part; FK → evidence, **Restrict** (SqlServer rejects two cascade paths from campaigns) |

Indexes: PK `(OpportunityId, EvidenceId)`; `EvidenceId`. Not owner-stamped.

### activities — `Domain/Opportunities/Activity.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| OpportunityId | uuid | FK → opportunities, Cascade |
| Kind | string(32) | plain string, not an enum: `StatusChanged`, `Applied`, `Researched` (`ActivityKinds`) |
| OccurredAt | time | |
| Detail | string(500) | truncated with "…" |

Indexes: `(OpportunityId, OccurredAt)`; `OwnerId`.

### outreach_drafts — `Domain/Drafts/OutreachDraft.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| OpportunityId | uuid | FK → opportunities, Cascade |
| Channel | string(32) | `DraftChannel`; CoverNote is the implemented generator |
| Recipient | string(320), null | |
| RecipientVerified | bool | false for the current CoverNote flow |
| Subject | string(300), null | |
| Body | string(10000) | exact user-editable draft text |
| Version | int | concurrency token; starts at 1 |
| State | string(32) | Draft or Approved |
| ApprovedHash | string(64), null | SHA-256 hex of exact approved content |
| ApprovedVersion | int, null | |
| ApprovedAt | time, null | |
| Source | string(32) | Template or Gemini (current generator is Template) |
| ClaimsJson | json | `{ text, basis, evidenceId? }[]` |
| CreatedAt, UpdatedAt | time | |

Indexes: unique `(OpportunityId, Channel)`; `(OwnerId, OpportunityId, UpdatedAt)`; `(OwnerId, State, UpdatedAt)`.

### sales_projects — `Domain/Sales/SalesProject.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| Source | string(32) | `Freelancer`, `TenderFeed`, `PublicUrl` or `Manual` |
| ExternalId | string(200), null | Provider id; unique per owner/source when present |
| Title | string(300) | |
| Buyer | string(300), null | |
| Description | string(8000), null | |
| Url | string(1000), null | |
| DeadlineUtc | time, null | |
| State | string(32) | `New`, `Shortlisted`, `BidPrepared`, `BidApproved`, `BidPlaced`, `ManualHandoff`, `Dismissed` |
| EvidenceJson | json | Evidence references; no inferred claims |
| Version | int | concurrency token |
| CreatedAt, UpdatedAt | time | |

Indexes: unique `(OwnerId, Source, ExternalId)` when `ExternalId` is present; `(OwnerId, UpdatedAt)`.

### sales_bids — `Domain/Sales/SalesBid.cs`

| Column | Type | Notes |
|---|---|---|
| Id, OwnerId | uuid | |
| ProjectId | uuid | FK → sales_projects, Cascade |
| Amount | decimal(18,2) | User-supplied; never inferred |
| Currency | string(3) | ISO alphabetic code |
| DeliveryDays | int | User-supplied, 1–3650 |
| Proposal | string(10000) | exact versioned text |
| ClaimsJson | json | claim basis/evidence |
| Version | int | concurrency token; starts at 1 |
| State | string(32) | Draft, Approved, Placed or Failed |
| ApprovedHash | string(64), null | SHA-256 of exact amount, timing, text and version |
| ApprovedVersion, ApprovedAt | int/time, null | |
| CreatedAt, UpdatedAt | time | |

Indexes: `(OwnerId, ProjectId, UpdatedAt)`; `ProjectId`.

## Delete behaviour

| From | To | Behaviour |
|---|---|---|
| campaigns | sources, import_batches, research_jobs, evidence, opportunities | Cascade |
| sources | source_items | Cascade (and deleted explicitly by `SourceService`, because InMemory runs no cascades) |
| research_jobs | research_events | Cascade |
| opportunities | opportunity_evidence, activities, outreach_drafts | Cascade |
| sales_projects | sales_bids | Cascade |
| evidence | opportunity_evidence | Restrict |
| profiles | campaigns | Restrict |

No endpoint deletes profiles, campaigns or opportunities today (see [open-questions.md](open-questions.md)).

## Provider differences

| Concern | SqlServer (`SqlServerAppDbContext`, local LocalDB) | Postgres (`PostgresAppDbContext`, Supabase and tests) | InMemory (`InMemoryAppDbContext`, demo mode) |
|---|---|---|---|
| JSON columns | `nvarchar(max)` | `jsonb` (11 mapped properties across research, drafts, and sales: StructuredDataJson, CriteriaJson, WeightsJson, RowsJson, CountsJson, BreakdownJson, FactsJson, GapsJson, ClaimsJson, EvidenceJson) | text |
| Strings > 4000 | `nvarchar(max)` (Source.Text, SourceItem.Description) | `character varying(n)` | — |
| DateTime | `datetime2` + UTC converter | `timestamp with time zone` | — |
| Filtered unique indexes | `[ExternalId] IS NOT NULL`, `[State] IN (N'Queued', N'Running')` | `"ExternalId" IS NOT NULL`, `"State" IN ('Queued', 'Running')` | **not enforced** |
| Unique indexes, FKs, cascades | enforced | enforced | **not enforced**: services check duplicates and delete children themselves |
| Migrations | `Persistence/Migrations/SqlServer` | `Persistence/Migrations/Postgres` | none (`EnsureCreated` is not used either; the model is used directly) |

## Migrations

Both sets must contain the same logical migrations in the same order.

| # | Name | SqlServer file | Postgres file | Creates |
|---|---|---|---|---|
| 1 | InitialProfiles | `20260930120109_InitialProfiles` | `20260930120115_InitialProfiles` | schema `app`, profiles |
| 2 | AddApplicationsAndAgentKeys | `20261001065453_AddApplicationsAndAgentKeys` | `20261001065506_AddApplicationsAndAgentKeys` | job_applications, agent_keys |
| 3 | AddResearchPipeline | `20261001105144_AddResearchPipeline` | `20261001105153_AddResearchPipeline` | campaigns, sources, source_items, import_batches, research_jobs, research_events, evidence, opportunities, opportunity_evidence, activities |
| 4 | AddAutoSuggest | `20261005062559_AddAutoSuggest` | `20261005062613_AddAutoSuggest` | nullable `campaigns.AutoSuggestMinScore` |
| 5 | AddOutreachDrafts | `20261006100640_AddOutreachDrafts` | `20261006100654_AddOutreachDrafts` | outreach_drafts |
| 6 | AddSalesPipeline | `20261006105733_AddSalesPipeline` | `20261006105746_AddSalesPipeline` | sales_projects, sales_bids |

The remaining M4/M5 persistence from [M4_M5_CONTRACT.md](M4_M5_CONTRACT.md) is not built:
Suppression, NextAction and UsageRecord.
