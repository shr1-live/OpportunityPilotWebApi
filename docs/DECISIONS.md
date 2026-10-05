# Decisions

Short records of choices that shape the code. Newest last.

## 2026-10-01 — Research first slice: Job and Customer only

The shared pipeline (sources → evidence → hard filters → scoring → opportunities) is built once and proven on two
modes. Job serves the user's immediate goal (the desktop agent applies to shortlisted jobs); Customer is the plan's
primary workflow. Partner, Investor and Freelance return 400 "not supported yet" until M7 rather than being scored
with rules that were never designed for them.

## 2026-10-01 — Deterministic rules engine with fixed default weights

Rules (keyword matching on word boundaries, a few skill aliases, experience-range regexes, work-mode keywords) run
before any AI. They are pure functions in `Application/Research/Rules`, unit-tested per criterion, and versioned with
the code: changing a pattern or alias changes scores. Weights have fixed per-mode defaults, are editable per campaign and
are stored as integers summing to exactly 100, so a breakdown always adds up. Hard filters run before scoring and an
unknown is never a pass. The exact values and rules: [business-rules.md](business-rules.md#scoring).

## 2026-10-01 — Not applicable is redistributed; unknown is not

A criterion the user configured nothing for (no preferred skills, no years of experience) is left out and its weight is
spread proportionally over the rest, so a campaign that does not care about experience is not capped at 80. A criterion
that *is* configured but that the source does not answer stays in, scores 0 and lowers coverage, so sparse sources
cannot look like strong matches (plan §12: "do not renormalize away unknowns"). Contact path has nothing to configure
and is always applicable in Customer campaigns.

## 2026-10-01 — Durable jobs with leases, driven by an injectable runner

`ResearchJob` rows are the queue. A single hosted processor claims one job by setting a two-minute lease guarded by the
row's version (optimistic concurrency, so no provider-specific SQL — it works on Postgres, SQL Server and InMemory),
renews it after each source, and an expired lease is reclaimed after a crash or restart. A job interrupted three
times is failed instead of retried forever. Progress saves merge the user's concurrent changes (cancel requests,
status changes) instead of overwriting them. Opportunities are upserted by `(campaign, dedupe key)` and research never
changes their status, so reruns are safe. Tests disable the hosted loop and call `IResearchRunner` directly.

## 2026-10-01 — Safe fetching is not configurable

The fetcher resolves DNS inside the connect callback, refuses any answer containing a non-public address, and connects
to the address it checked (DNS rebinding cannot slip through). Redirects are followed by hand, each hop re-checked.
http is allowed only in Development; ports 80/443 only. The address policy is a DI service with one production
implementation and no configuration binding; the only relaxed policies live in the test project.

## 2026-10-01 — The desktop agent as a research source

LinkedIn and Naukri are never crawled server-side. The agent reads postings in the user's own logged-in browser and
delivers them into one Agent source per campaign and platform, keyed by the platform's job id. Research scores them like
any other source. The agent is offered only opportunities the user shortlisted, and an Applied report with the
opportunity id moves it to Applied. Agent keys work only on the agent endpoints and the report endpoint.

## 2026-10-01 — Demo mode keeps research working

Without a connection string the API uses the EF InMemory store; the processor and rules run unchanged, because claiming
relies on concurrency tokens rather than raw SQL. Unique indexes are not enforced there, so services check for
duplicates themselves. Data still disappears on restart, and capabilities say so.
