# Retention, cleanup, archive and export

`RetentionService` runs in the API (`RetentionProcessor`): 3 minutes after start, then every 6 hours, 5 minutes
apart while a batch was full. Each pass deletes at most 500 rows per kind across all owners. Demo mode (no database)
skips it — in-memory data disappears on restart anyway.

| Data | Kept for | Why it goes |
|---|---|---|
| Import previews not committed | until their expiry (`ImportBatch.Lifetime`) | a preview that was never committed has no further use |
| Research run events (the step log of a run) | 90 days | runs, counts, opportunities and evidence stay; only the step-by-step log goes |
| AI usage rows | 90 days | the daily limit only needs today; 90 days covers billing questions |
| Security events | 365 days | one year of key and data-export history |
| Guest sessions | until they expire (30 days) | an expired session can never sign in again |

Never removed by retention: profiles and their versions, campaigns, sources, research jobs and their input snapshots,
opportunities, evidence, drafts, applications, outreach, schedules, Wellfound and Sales records, and every staffing
record (accounts, contacts, deals and their activity, candidates, submissions, interviews, feedback, offers, rate cards,
proposals, messages, meetings). Those are removed only by the owner's **Settings → Delete all data**
(`DELETE /api/v1/account-data`, which also removes the owner's AI usage and security events and keeps one
"AccountDataDeleted" record with no content).

**Export.** `GET /api/v1/account-data/export` returns every table above for the signed-in owner as JSON (Settings →
Export JSON). There is no import/restore endpoint: an export is a copy for the owner's records, not a backup to load
back. Database backups are Supabase's (daily on the free plan); restoring one restores everyone's data to that point.

**Archive.** The optional MongoDB research archive (M8, `Features__MongoArchiveEnabled`) is not built; nothing is
archived outside the main database.
