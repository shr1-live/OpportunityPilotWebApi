# Candidate phase 1 contract: open job sources + batch approval queue

> Design spec for one slice (branch `feature/candidate-sources-approvals`). After merge, `docs/api-contracts.md`,
> `docs/db-schema.md`, `docs/business-rules.md` and `docs/opportunity-lifecycle.md` are authoritative.

User decisions (2026-10-05, see HANDOFF): the **candidate** flow is built first; sources that don't
depend on logged-in platform automation become the main way in; automation level is **batch
approval** — the app suggests, the user approves a list in one click.

## 1. New research sources (Job campaigns only)

`SourceKind` gains `Greenhouse`, `Lever`, `Adzuna`. `JobPlatform` gains `Greenhouse`, `Lever`, `Adzuna`
(stored as strings; no column change). All three are fetched by the server through the existing
`SafeFetcher` (HTTPS, address validation, size cap) with `application/json` added to the content
types **only for these source kinds**. Customer campaigns reject them with 400.

| Kind | Source fields used | Fetch | Map to candidate |
|---|---|---|---|
| Greenhouse | `Url` = board token (e.g. `stripe`) **or** a `boards.greenhouse.io/<token>` / `job-boards.greenhouse.io/<token>` URL (normalised to the token, `[a-z0-9-]{1,100}`) | 1) `GET https://boards-api.greenhouse.io/v1/boards/{token}/jobs` (no content — big boards are hundreds of jobs). 2) Pre-filter titles: keep jobs whose title contains any campaign `keywords` word or any `requiredSkills`; if none configured keep all. 3) For the first N kept (N = remaining fetch budget, ≤ `Research:MaxFetches`) `GET …/jobs/{id}` for `content` | title, organization = `company_name` or the token, location = `location.name`, url/applyUrl = `absolute_url`, externalId = `id`, description = HTML-unescaped `content` → text (ContentParser), platform Greenhouse |
| Lever | `Url` = company slug **or** `jobs.lever.co/<slug>` URL (normalised, `[a-z0-9-]{1,100}`) | `GET https://api.lever.co/v0/postings/{slug}?mode=json&limit=100` | title = `text`, organization = slug (title-cased) , location = `categories.location`, workMode hint = `workplaceType` (`remote`/`hybrid`/`on-site`), url = `hostedUrl`, applyUrl = `applyUrl`, externalId = `id`, description = `descriptionPlain` + list items text, platform Lever |
| Adzuna | none (uses campaign `keywords` and the first non-"Remote" `locations` entry) | `GET https://api.adzuna.com/v1/api/jobs/in/search/1?app_id=…&app_key=…&what={keyword}&where={location}&results_per_page=50&max_days_old={postedWithinDays default 14}&content-type=application/json` per keyword (≤ 3 keywords) | title, organization = `company.display_name`, location = `location.display_name`, url/applyUrl = `redirect_url`, externalId = `id`, description = `description` (a snippet — coverage will be lower; that's honest), platform Adzuna |

- Adzuna needs server config `Adzuna:AppId` and `Adzuna:AppKey` (free developer keys). Without them the
  source is accepted but fails safely at fetch time with "Adzuna is not configured on the server" and the
  job is CompletedWithGaps; capability `adzuna` reports NotConfigured. Keys are never logged or returned
  (the request URL with the key must not appear in events, SafeError or logs).
- Creating a source validates the token/slug format; it does not call the API.
- A source can be added more than once with different tokens (one board per source). Max 20 sources per campaign (existing cap).
- Dedupe key for these: `job:{platform}:{externalId}` (existing rule).
- Apply path: Greenhouse/Lever/Adzuna opportunities are applied to by the **user** via `applyUrl`
  (the agent only handles LinkedIn/Naukri). The UI's "Open application page" + "Mark applied" covers it.
- Capabilities: add `greenhouse` (Ready: "Public job boards of companies that use Greenhouse; no key needed"),
  `lever` (Ready, same idea), `adzuna` (Ready when keys present, else NotConfigured), category `Sources`.
- Business rule doc: these are documented public APIs meant for job listings; no login, no scraping.

`POST /api/v1/campaigns/{id}/sources` body accepts `{ kind: "Greenhouse" | "Lever", url: "<token, slug or board URL>", label? }`
and `{ kind: "Adzuna", label? }`. `Source` DTO unchanged (url holds the normalised token/slug).

## 2. Batch approval queue

- `OpportunityStatus` gains **`Suggested`** (between New and Shortlisted).
- `Campaign` gains **`AutoSuggestMinScore`** (`int?`, 1–100, null = off). Exposed in Campaign DTOs as
  `autoSuggestMinScore` (create/update bodies accept it; PUT keeps the current value when omitted).
  Migration `AddAutoSuggest` (both providers): one nullable int column.
- After scoring, research moves an opportunity **New → Suggested** when: campaign mode is Job,
  `AutoSuggestMinScore` is set, outcome is **Qualified**, and score ≥ the threshold. This is the only
  status change research may make; it never touches any other status (Shortlisted/Applied/Dismissed stay).
  Adds an Activity `Suggested` ("Suggested for approval: scored 86 ≥ 80").
- Endpoints (user auth):

| Method | Path | Body / query | Response |
|---|---|---|---|
| GET | /api/v1/approvals | `campaignId?`, `take=100 (≤200)`, `skip` | `{ total, items: ApprovalItem[] }` — Suggested opportunities, highest score first |
| POST | /api/v1/approvals/decide | `{ approve: Guid[], reject: Guid[] }` (≤200 total, no id in both) | `{ approved, rejected, skipped }` — approve → Shortlisted, reject → Dismissed, each with an Activity (`Approved` / `Rejected`); ids not owned or no longer Suggested are counted as skipped, not errors |

```ts
ApprovalItem = { opportunityId, campaignId, campaignName, title, organization, location, platform,
  applyUrl, score, coverage, outcomeReason, appliesVia: 'Agent' | 'You' }   // Agent = LinkedIn/Naukri, You = open applyUrl
```
- PATCH status also allows a user to set `Suggested` back to `Shortlisted`/`Dismissed`/`New`.
- Overview gains `awaitingApproval` (count of Suggested) — appended to OverviewDto.
- The agent shortlist endpoint is unchanged (Shortlisted only), so nothing is applied before approval.

## 3. Out of scope for this slice
Cover notes / drafts (next feature PR, per M4_M5_CONTRACT), Gemini, Customer-mode suggestions, Freelancer.com.
