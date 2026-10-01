# Research pipeline contract (M2 + M3, Job and Customer modes)

Implements the implementation plan's research design (§6, §9, §11, §12, §16, §18) for two
modes first — **Job** and **Customer** — on the shared pipeline. Other modes
(Partner, Investor, Freelance) are rejected with 400 "not supported yet" until M7.

User decisions (2026-10-01): research feeds the local agent — jobs the user
*shortlists* are the only ones the agent applies to; the agent may also act as a
research **source** that collects LinkedIn/Naukri postings from the user's own
logged-in browser. Rules-based extraction and scoring now; Gemini plugs in at M4.

JSON is camelCase, enums are strings, errors are ProblemDetails with
`correlationId`, owner scope on everything (404 for others' IDs), `/api/v1`.

## Enums

| Enum | Values |
|---|---|
| OpportunityMode (exists) | Customer, Partner, Investor, Job, Freelance — only **Job** and **Customer** accepted for now |
| SourceKind | Paste, Csv, Url, Feed, Agent |
| SourceStatus | Pending, Ok, Failed, Skipped |
| ResearchJobState | Queued, Running, Completed, CompletedWithGaps, Failed, Cancelled |
| ResearchStage | Prepare, Gather, Extract, Filter, Score, Complete |
| FilterOutcome | Qualified, NeedsVerification, Excluded |
| OpportunityStatus | New, Shortlisted, Dismissed, Applied, Contacted, Responded, Interested, Closed |
| JobPlatform | LinkedIn, Naukri, Other |
| EventLevel | Info, Warning, Error |

## Criteria and weights

```jsonc
// CampaignCriteria — every list may be empty (= not applied)
{
  "keywords": [],              // job: titles/search phrases; customer: product/search words. Used by the agent's searches.
  "requiredSkills": [],        // Job: mandatory skills (hard filter: all known-missing → Excluded)
  "preferredSkills": [],       // Job
  "candidateYears": null,      // Job: the user's confirmed years of experience (number or null)
  "locations": [],             // Job + Customer: accepted places; "Remote" also counts as a location token
  "workModes": [],             // Job: subset of ["Remote","Hybrid","Onsite"]
  "industries": [],            // Customer
  "problems": [],              // Customer: business problems the product solves (keywords)
  "signals": [],               // Customer: published signals, e.g. "hiring", "funding", "expansion"
  "excludeKeywords": [],       // both: any match in title/text → Excluded
  "excludeOrganizations": []   // both: org name contains → Excluded
}
```

Weights (editable per campaign; server normalises to sum 100, each 0–100):

| Mode | Criterion key → default |
|---|---|
| Job | `mandatorySkills` 40, `experience` 20, `location` 20, `preferredSkills` 20 |
| Customer | `industry` 25, `problem` 30, `geography` 15, `signal` 20, `contactPath` 10 |

## Scoring (plan §12)

Each criterion gets value **1, 0.5, 0 or null (unknown)** plus a reason and the
evidence IDs it relied on. `points = weight × value` (null → 0).
**Score** = Σ points over *applicable* criteria (0–100, integer).
**Coverage** = percentage of applicable weight whose value is not null.
A criterion is *not applicable* (omitted, weight redistributed proportionally among
applicable ones) only when the user configured nothing for it (e.g. no
preferredSkills, no candidateYears). Unknown ≠ not applicable: unknown stays in
and contributes 0.

Job rules:
- mandatorySkills: fraction of requiredSkills found in posting text (word-boundary, case-insensitive, aliases: "c#"≈"csharp", ".net"≈"dotnet", "javascript"≈"js", "typescript"≈"ts"). All → 1; ≥50% → 0.5; else 0. Text empty → null.
- preferredSkills: same fractions over preferredSkills.
- experience: posted range parsed from text (`3-5 years`, `3 to 5 yrs`, `5+ years`, `minimum 4 years`). candidateYears inside range → 1; within 1 year outside → 0.5; further → 0; no range found → null.
- location: posting location/text matches any of `locations` (or work mode Remote when locations contains "Remote") → 1; known location not matching → 0; unknown → null. workModes: if configured and the posting's detected mode (remote/hybrid/on-site keywords) is not in the list → 0; undetected → contributes nothing extra.

Customer rules:
- industry: any `industries` keyword in text → 1; else if text non-empty → 0; empty → null.
- problem: ≥2 `problems` keywords or 1 of a single configured → 1; 1 of several → 0.5; none → 0; empty text → null.
- geography: country/city/location field or text matches `locations` → 1; known other → 0; unknown → null.
- signal: any `signals` keyword → 1; none → null (absence of a published signal is unknown, not negative — plan §12).
- contactPath: a website/careers/contact URL or "contact us"/email in text → 1; none → null.

## Hard filters (before scoring)

Pass / Fail / Unknown per filter. Any **Fail** → `Excluded` (reason recorded). Any
**Unknown** on a configured hard filter → `NeedsVerification`. All pass →
`Qualified`. Hard filters: excludeKeywords, excludeOrganizations, Job
requiredSkills (Fail only if text is present and *none* of the required skills
appear), Job workModes (Fail if a detected mode is outside the list), locations
(Fail if a known location matches none; Unknown if no location found).

## Entities (all owned, UUID, UTC)

- **Campaign**: OwnerId, ProfileId (must be the owner's), Mode, Name ≤200, Goal ≤2000, CriteriaJson, WeightsJson, ResultLimit (1–100, default 25), Version (concurrency), CreatedAt, UpdatedAt.
- **Source**: OwnerId, CampaignId, Kind, Label ≤200, Url ≤1000?, Text (Paste, ≤50 000 chars)?, PermissionNote ≤500?, Platform? (Agent sources), Status, LastFetchedAt?, SafeError ≤500?, CreatedAt. CSV and Agent sources hold their rows in **SourceItem** (OwnerId, SourceId, ExternalId ≤100?, Title ≤300, Organization ≤300, Location ≤200?, Url ≤1000?, Description ≤20 000?, Website ≤1000?, Country ≤100?, Industry ≤200?, unique (SourceId, ExternalId) when ExternalId present).
- **ImportBatch** (CSV preview, pending): OwnerId, CampaignId, RowsJson, CreatedAt, ExpiresAt (1 h), Committed.
- **ResearchJob**: OwnerId, CampaignId, State, Stage, CountsJson, Attempts, LeaseUntil?, CancelRequested, CreatedAt, StartedAt?, FinishedAt?, SafeError?, Version.
- **ResearchEvent**: JobId, At, Stage, Level, Message ≤500 (safe summary, no stack traces/keys).
- **Evidence**: OwnerId, CampaignId, SourceId, Url?, RetrievedAt, ContentHash (sha256 hex of excerpt), Excerpt ≤2000, ExtractionMethod ("Rules").
- **Opportunity**: OwnerId, CampaignId, Mode, DedupeKey ≤400 (unique per CampaignId), Title ≤300, Organization ≤300, Location ≤200?, Url ≤1000?, ApplyUrl ≤1000?, Platform (JobPlatform)?, ExternalId ≤100?, Description excerpt ≤4000?, Score (int), Coverage (int), Outcome, OutcomeReason ≤500?, Status (default New), BreakdownJson, FactsJson, GapsJson, LastResearchJobId, CreatedAt, UpdatedAt, Version.
- **OpportunityEvidence**: OpportunityId, EvidenceId (link table).
- **Activity**: OwnerId, OpportunityId, Kind ("StatusChanged","Applied","Researched"), OccurredAt, Detail ≤500.

Dedupe key: Job → `job:{platform}:{externalId}` when both known, else normalised `title|organization`; Customer → normalised website domain when known, else normalised name. Re-running research upserts (updates score/evidence), never duplicates, and **never resets Status** (a Shortlisted/Applied opportunity stays so).

## Endpoints (user auth)

| Method | Path | Body / query | Response |
|---|---|---|---|
| GET | /api/v1/campaigns | — | `CampaignSummary[]` newest first |
| POST | /api/v1/campaigns | `{ profileId, mode, name, goal, criteria, weights?, resultLimit? }` | 201 `Campaign` |
| GET | /api/v1/campaigns/{id} | — | `Campaign` |
| PUT | /api/v1/campaigns/{id} | `{ name, goal, criteria, weights, resultLimit, expectedVersion }` | `Campaign`; 409 stale |
| GET | /api/v1/campaigns/{id}/sources | — | `Source[]` |
| POST | /api/v1/campaigns/{id}/sources | `{ kind: "Paste"\|"Url"\|"Feed", label?, url?, text?, permissionNote? }` | 201 `Source` |
| DELETE | /api/v1/campaigns/{id}/sources/{sourceId} | — | 204 |
| POST | /api/v1/imports/preview | `{ campaignId, csv }` (≤1 MB, ≤1000 rows) | `ImportPreview` |
| POST | /api/v1/imports/{importId}/commit | `{ label? }` | 201 `Source` (Kind Csv) |
| POST | /api/v1/campaigns/{id}/research | — | 202 `{ jobId }`; if a job for this campaign is Queued/Running, 202 with that job's id (no duplicate) |
| GET | /api/v1/campaigns/{id}/research-jobs | — | `ResearchJob[]` newest first (max 20, events omitted) |
| GET | /api/v1/research-jobs/{id} | — | `ResearchJob` with `events` (latest 100, newest first) |
| POST | /api/v1/research-jobs/{id}/cancel | — | 202 `ResearchJob` |
| GET | /api/v1/campaigns/{id}/opportunities | `outcome?, status?, sort=score\|recent, take=50 (≤200), skip=0` | `{ total, items: OpportunitySummary[] }` |
| GET | /api/v1/opportunities/{id} | — | `OpportunityDetail` |
| PATCH | /api/v1/opportunities/{id}/status | `{ status }` user may set New, Shortlisted, Dismissed, Applied (manual confirmation), Contacted, Responded, Interested, Closed | `OpportunityDetail` + Activity row |
| GET | /api/v1/campaigns/{id}/export | — | `text/csv` (cells starting with = + - @ tab CR prefixed with `'`) |

CSV columns (header row required, case-insensitive): **Job** requires `title` and `company`; optional `location`, `url`, `description`, `id`. **Customer** requires `name`; optional `website`, `country`, `industry`, `description`. Unknown columns → a preview warning, ignored.

## Endpoints (agent key only — `X-Agent-Key`)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /api/v1/agent/campaigns | — | `[{ id, name, mode, criteria }]` the owner's Job-mode campaigns |
| POST | /api/v1/agent/campaigns/{id}/postings | `{ platform: "LinkedIn"\|"Naukri", items: [{ externalId, url, title, company, location?, description? }] (1–100), queueResearch: bool }` | `{ accepted, sourceId, jobId? }` — upserts into that campaign's Agent source for the platform (one per platform), items deduped by externalId; queues research when asked |
| GET | /api/v1/agent/shortlist | `?platform=LinkedIn` | `[{ opportunityId, campaignId, platform, externalId, url, title, organization }]` — Job opportunities with Status Shortlisted, that platform, not yet Applied |
| POST | /api/v1/applications/report | existing; items gain optional `opportunityId` | when an item with `opportunityId` (owned) has status Applied → opportunity Status Applied + Activity "Applied" |

## DTO shapes

```ts
CampaignSummary = { id, profileId, mode, name, goal, resultLimit, version, createdAt, updatedAt,
  sourceCount: number, opportunityCount: number, lastJob: { id, state, stage, finishedAt } | null }
Campaign = CampaignSummary & { criteria: CampaignCriteria, weights: Record<string, number> }
Source = { id, campaignId, kind, label, url, platform, permissionNote, status, lastFetchedAt,
  safeError, itemCount: number, textLength: number, createdAt }
ImportPreview = { importId, columns: string[], warnings: string[],
  rows: { row: number, values: Record<string,string>, errors: string[] }[]  /* first 50 */,
  validCount, errorCount }
ResearchJob = { id, campaignId, state, stage, createdAt, startedAt, finishedAt, safeError,
  counts: { sources, sourcesDone, sourcesFailed, fetched, candidates, qualified, needsVerification, excluded },
  events?: { at, stage, level, message }[] }
OpportunitySummary = { id, campaignId, mode, title, organization, location, url, applyUrl, platform,
  score, coverage, outcome, outcomeReason, status, gapsCount, updatedAt }
OpportunityDetail = OpportunitySummary & { description, version,
  breakdown: { criterion, label, weight, value: 1|0.5|0|null, points, reason, evidenceIds: string[] }[],
  facts: { key, label, value, evidenceId, isInference }[],
  gaps: string[],
  evidence: { id, sourceId, sourceLabel, url, retrievedAt, excerpt, extractionMethod }[],
  activities: { kind, occurredAt, detail }[] }
```

## Research job (plan §18)

- `POST …/research` persists a Queued job and returns immediately.
- A single hosted `ResearchProcessor` (BackgroundService) polls every 2 s, claims one job
  atomically (Queued, or Running with expired lease) by setting a 2-minute lease
  with the job's concurrency token, renews it between batches, and checks
  `CancelRequested` between items (partial results kept; state Cancelled).
- Stages and events: Prepare (criteria summary) → Gather (per source: fetched N
  items / failed safely) → Extract → Filter → Score → Complete. Counts persisted
  after each source.
- Limits (config `Research:*`, server ceilings): MaxCandidates 100 per run (and
  campaign ResultLimit caps *new* opportunities written, highest score first),
  MaxFetches 50, 1 MB per page, 10 s timeout, 2 concurrent fetches, 3 transient
  retries with jittered backoff, ≤5 redirects.
- Final state: Completed, or CompletedWithGaps if any source failed, Failed only on
  an unexpected error (SafeError, logged with correlation).
- On restart, expired leases are reclaimed; upserts by DedupeKey make reruns safe.

## Safe fetching (plan §11)

- Only `https` (and `http` only in Development), no credentials in URL, ports 80/443 only.
- Resolve DNS and reject loopback, private (10/8, 172.16/12, 192.168/16), link-local
  (169.254/16 incl. metadata), CGNAT 100.64/10, multicast, reserved, IPv6
  loopback/ULA/link-local, and IPv4-mapped forms of those. Validate the **actual
  connected address** (SocketsHttpHandler.ConnectCallback) so DNS rebinding fails.
- Redirects handled manually; each hop re-validated; max 5.
- Accept text/html, application/xhtml+xml, text/plain, application/rss+xml,
  application/atom+xml, application/xml, text/xml. Others rejected.
- Read at most 1 MB (decompressed); strip scripts/styles/nav/footer; collapse text;
  keep ≤20 000 chars for extraction, ≤2000-char excerpt as Evidence.
- Pages that yield < 200 chars of text → source item marked "NeedsManualInput"
  (dynamic page) with an event, not an error.
- Feeds: RSS 2.0 and Atom; each item → a candidate (title, link, description/summary text,
  published date). Max 100 items per feed.
- No logged-in crawling server-side. LinkedIn/Naukri pages are reached only through
  the user's local agent (Agent source).

## Paste source

Plain text, ≤50 000 chars. Multiple postings/companies separated by a line containing
only `---`. First non-empty line = title (Job) / name (Customer); a line
`Company: X` / `Location: Y` / `Website: Z` / `URL: …` sets those fields; the rest is
the description.
