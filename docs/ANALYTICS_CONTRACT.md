# Analytics contract (redesign R2a) — overview metrics for the Candidate and Sales workspaces

> Design spec for one slice (branch `feature/analytics-api`). After merge, `docs/api-contracts.md` and
> `docs/business-rules.md` are authoritative. Drives the v2 overview designs
> (`opportunitypilot-ui/Main.dc.html`, `OverviewSales.dc.html`).

**Every number is computed from stored data for the calling owner. Nothing is estimated or sampled.**
Where the data does not exist (e.g. contacted/responded for Sales before Outreach), the field is `null`
and the UI shows "—" with the reason.

## Endpoint

`GET /api/v1/analytics/overview?workspace=Candidate|Sales&days=30` (user auth; agent keys rejected).
`workspace` maps to campaign mode: Candidate → `Job`, Sales → `Customer`. `days` 1–365, default 30; the
window applies to opportunity `CreatedAt`, application/activity times and research jobs (`CreatedAt`).

## Response

```ts
AnalyticsOverview = {
  workspace: 'Candidate' | 'Sales', days: number, generatedAt: string,
  campaignCount: number,                       // campaigns of that mode (all time)
  kpis: {
    found: number,                             // opportunities created in window
    qualified: number,                         // of those, outcome Qualified
    qualifyRate: number | null,                // qualified / found, 0–1, null when found = 0
    awaitingApproval: number,                  // status Suggested now (Candidate; Sales 0)
    shortlisted: number,                       // status Shortlisted now
    shortlistedNotApplied: number,             // Candidate: Shortlisted now with no Applied activity
    applied: number | null,                    // Candidate: opportunities that reached Applied in window; Sales: null
    appliedByAgent: number | null,             // Applied via agent report (Activity "Applied" from the agent)
    appliedByYou: number | null,               // Applied by the user's status change
    contacted: number | null,                  // Sales: null until Outreach exists; Candidate: null
    responded: number | null,                  // reached Responded (Activity/Status); null for Sales until Outreach
    respondedRate: number | null,              // responded / applied
    agentNeedsYou: number                      // Candidate: JobApplications NeedsManual in window
  },
  funnel: { key: string, label: string, count: number | null, note: string }[],
  //  Candidate: read, found, qualified, suggested, shortlisted, applied, responded
  //  Sales:     read, found, qualified, shortlisted, contacted (null), responded (null)
  //  "read" = sum over the window's completed research jobs of counts.candidates, latest job per campaign only
  //  (re-runs would otherwise double count). "reached X" = current status at or beyond X in the order
  //  New < Suggested < Shortlisted < Applied < Contacted < Responded < Interested, OR an Activity proving it
  //  reached X before being Dismissed/Closed.
  fitHistogram: { bands: { from: number, to: number, count: number }[],  // 10 bands 0–9 … 90–100, qualified only
                  threshold: number | null,     // most common AutoSuggestMinScore among this mode's campaigns
                  aboveThreshold: number | null },
  unknownCriteria: { criterion: string, label: string, unknownCount: number }[],  // top 5 by count, from
  //  breakdown rows with value null on Qualified + NeedsVerification opportunities in window
  sources: { sourceId: string, campaignId: string, label: string, kind: string, platform: string | null,
             read: number, qualified: number, rate: number | null, lastFetchedAt: string | null,
             failing: boolean }[],          // read = items the source yielded on its last fetch (or SourceItem count);
                                            // qualified = Qualified opportunities whose evidence comes from it;
                                            // failing = status Failed; ordered by read desc; max 20
  applicationsPerDay: { date: string, applied: number, replies: number }[] | null,  // Candidate: last 14 days in UTC
  attention: { kind: 'Approvals' | 'ShortlistedNotApplied' | 'AgentNeedsYou' | 'SourceFailing', count: number,
               detail: string }[],          // only entries with count > 0
  activeResearch: { jobId: string, campaignId: string, campaignName: string, state: string, stage: string,
                    counts: { candidates: number, sources: number, sourcesDone: number } } | null,  // latest Queued/Running
  // Sales only (null for Candidate):
  qualifiedByIndustry: { industry: string, count: number }[] | null,   // from the matched industry fact, top 6 + "Other"
  signalsFound: { signal: string, count: number }[] | null            // from matched signal facts
}
```

## Rules
- Owner-scoped; another owner's data never contributes. Empty account → zeros, empty lists, `null` rates.
- Computed with EF LINQ only (works in demo/InMemory mode); bounded queries (window + owner); no N+1 per source.
- Overview DTO is unchanged; the web app calls this endpoint for the v2 overview.
- Tests: unit tests for the funnel "reached" logic, histogram banding (boundary 70/100), rates with zero denominators;
  integration test that builds two campaigns + research + approvals + an agent Applied report and asserts every
  number, plus ownership isolation and `workspace=Sales` nulls.

## Update 2026-10-08 — Sales covers every Sales mode (P5)

- `GET /api/v1/analytics/overview?workspace=Sales&days=30[&mode=Customer|Partner|Investor|Freelance]`: Sales now counts
  all four Sales modes unless `mode` narrows it (400 for `mode` with `workspace=Candidate`).
- `kpis.contacted` / `kpis.responded` are counted for Sales from stored status changes (first reach of Contacted /
  Responded in the window); `respondedRate` = responded / contacted. Funnel `contacted` and `responded` are no longer null.
- New `outreach` (Sales only, counted now): `draftsAwaitingReview`, `draftsApproved`, `bidsPlaced`, `bidsFailed`,
  `followUpsDue` (open, due in the next 7 days), `followUpsOverdue`, `replyRate`.
- New attention kind `FollowUpsOverdue`.
