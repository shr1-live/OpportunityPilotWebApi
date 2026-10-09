# Where the data lives, and how to look at it

Everything the app creates is stored in the **production Postgres database on Supabase** (the API on Render connects with
`ConnectionStrings__Main`). Nothing important lives in the browser or in memory. The live diagnostics confirm it
(`GET /api/v1/diagnostics`, 2026-10-09): `provider: Postgres, reachable: true, pendingMigrations: 0`.

- All tables are in the schema **`app`** (not `public`). The Supabase client API cannot reach it: `anon` and `authenticated`
  have no usage on schema `app`, so only the API (with the server connection string) reads and writes.
- Each user's rows carry their `OwnerId`; every query in the API filters on it. Guests are real owners, kept in `guest_sessions`.
- Schema changes are EF Core migrations (`src/.../Persistence/Migrations/Postgres`), applied on startup; CI fails if the model and migrations differ.

## Tables by area

| Area | Tables |
|---|---|
| People and keys | `guest_sessions`, `agent_keys`, `security_events`, `ai_usage` |
| Profiles | `profiles`, `profile_versions` |
| Campaigns and research | `campaigns`, `sources`, `source_items`, `import_batches`, `research_jobs`, `research_events`, `campaign_schedules` |
| Results | `opportunities`, `evidence`, `opportunity_evidence`, `activities`, `next_actions` |
| Outreach | `outreach_drafts`, `provider_executions`, `suppressions` |
| Applications | `job_applications`, `wellfound_jobs`, `wellfound_applications`, `wellfound_activities` |
| Staffing | `staffing_accounts`, `staffing_contacts`, `staffing_deals`, `staffing_deal_activities`, `staffing_candidates`, `staffing_submissions`, `staffing_interviews`, `staffing_feedback`, `staffing_offers`, `staffing_rate_cards`, `staffing_proposals`, `staffing_messages`, `staffing_meetings` |
| Sales (removed from the UI, data kept) | `sales_projects`, `sales_bids`, `upwork_opportunities` |

## Look at it (Supabase → SQL editor, runs as the project owner)

```sql
select count(*) from app.opportunities;
select title, organization, score, status, "UpdatedAt" from app.opportunities order by "UpdatedAt" desc limit 20;
select state, count(*) from app.outreach_drafts group by state;          -- drafts waiting / approved / sent
select name, "NextRunAt", "Paused", "LastQueuedAt" from app.campaign_schedules;  -- scheduled campaigns
select operation, state, "CompletedAt" from app.provider_executions order by "CompletedAt" desc limit 20;
```

(Column names are the EF property names in quotes where Postgres kept their capitals; `\d app.opportunities` shows them.)
