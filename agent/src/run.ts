import type { Page } from 'playwright'
import { report } from './api.ts'
import { captureDebug, openBrowser } from './browser.ts'
import type { Config, SearchConfig } from './config.ts'
import type { JobRef, Outcome, PlatformAdapter } from './platforms/types.ts'
import type { ApplicationRecord, Store } from './store.ts'
import { between, log, sleep, StopRun } from './util.ts'

export interface RunOptions {
  adapter: PlatformAdapter
  config: Config
  store: Store
  submit: boolean
  limit?: number
  headless?: boolean
  profileDir?: string
  /** Lets tests drive an already-open page. */
  page?: Page
}

export interface RunSummary {
  outcomes: (Outcome & { job: JobRef })[]
  stoppedBecause: string | null
}

/** Why a job is skipped before opening the application, or null if it passes the user's filters. */
export function filterReason(job: JobRef, search: SearchConfig): string | null {
  const title = job.title.toLowerCase()
  const company = job.company.toLowerCase()
  const excluded = search.titleExclude.find((w) => w.trim() && title.includes(w.toLowerCase()))
  if (excluded) return `Title contains excluded word "${excluded}"`
  if (search.titleMustIncludeAny.length && !search.titleMustIncludeAny.some((w) => title.includes(w.toLowerCase())))
    return 'Title has none of your required words'
  const blocked = search.excludeCompanies.find((c) => c.trim() && company.includes(c.toLowerCase()))
  if (blocked) return `Company "${blocked}" is on your exclude list`
  return null
}

export async function runAgent(opts: RunOptions): Promise<RunSummary> {
  const { adapter, config, store } = opts
  const summary: RunSummary = { outcomes: [], stoppedBecause: null }
  const remainingToday = config.limits.maxApplicationsPerDay - store.appliedToday()
  const limit = Math.min(opts.limit ?? config.limits.maxApplicationsPerRun, opts.submit ? remainingToday : Infinity)
  if (limit <= 0) {
    summary.stoppedBecause = `Daily limit of ${config.limits.maxApplicationsPerDay} applications already reached.`
    log(summary.stoppedBecause)
    return summary
  }

  const browser = opts.page ? null : await openBrowser({ headless: opts.headless, profileDir: opts.profileDir })
  const page = opts.page ?? browser!.page
  let syncWorking = Boolean(config.api.url && config.api.key)
  let attempts = 0
  let consecutiveFailures = 0
  const seen = new Set<string>()

  const record = async (job: JobRef, outcome: Outcome) => {
    const r: ApplicationRecord = {
      platform: adapter.platform,
      externalJobId: job.externalJobId,
      jobUrl: job.url,
      title: job.title || '(untitled)',
      company: job.company || '(unknown company)',
      location: job.location,
      status: outcome.status,
      detail: outcome.detail,
      occurredAt: new Date().toISOString(),
      synced: false,
    }
    store.add(r)
    summary.outcomes.push({ ...outcome, job })
    log(`${outcome.status.padEnd(11)} ${job.title} — ${job.company}${outcome.detail ? ` (${outcome.detail})` : ''}`)
    if (syncWorking) {
      const result = await report(config.api, [r])
      if (result.ok) store.markSynced([r])
      else {
        syncWorking = false
        log(`Not syncing to the web app for the rest of this run: ${result.reason}. Results stay in the local log; run "npm run agent -- sync" later.`)
      }
    }
  }

  try {
    if (!(await adapter.isLoggedIn(page))) throw new StopRun(`Not logged in to ${adapter.platform}. Run: npm run agent -- login ${adapter.platform.toLowerCase()}`)
    log(`${opts.submit ? 'SUBMIT mode — real applications will be sent' : 'DRY RUN — forms are filled but nothing is sent'}. Limit this run: ${limit}.`)

    search: for (const keyword of config.search.keywords) {
      for (let p = 0; p < config.limits.maxPages; p++) {
        const jobs = await adapter.searchPage(page, config.search, keyword, p)
        log(`"${keyword}" page ${p + 1}: ${jobs.length} job(s)`)
        if (jobs.length === 0) break

        for (const listed of jobs) {
          if (attempts >= limit) break search
          const key = `${adapter.platform}:${listed.externalJobId}`
          if (seen.has(key) || store.isDone(adapter.platform, listed.externalJobId)) continue
          seen.add(key)

          const preFilter = filterReason(listed, config.search)
          if (preFilter && listed.title) {
            await record(listed, { status: 'Skipped', detail: preFilter })
            continue
          }

          let job = listed
          try {
            job = await adapter.openJob(page, listed)
            const reason = filterReason(job, config.search)
            if (reason) {
              await record(job, { status: 'Skipped', detail: reason })
              continue
            }
            const outcome = await adapter.apply(page, job, {
              submit: opts.submit,
              answers: config.answers,
              followCompanies: config.profile.followCompanies,
              resumePath: config.profile.resumePath,
            })
            await record(job, outcome)
            consecutiveFailures = outcome.status === 'Failed' ? consecutiveFailures + 1 : 0
            if (outcome.status === 'Applied' || outcome.status === 'DryRun') attempts++
          } catch (e) {
            if (e instanceof StopRun) throw e
            const evidence = await captureDebug(page, `${adapter.platform}-${job.externalJobId}-error`)
            await record(job, { status: 'Failed', detail: `${(e as Error).message.split('\n')[0]} (evidence: ${evidence}.png)` })
            consecutiveFailures++
          }
          if (consecutiveFailures >= 3)
            throw new StopRun(`Three applications in a row failed — the site layout has probably changed. Evidence is in .data/debug.`)

          const [min, max] = config.limits.pauseSeconds
          if (max > 0) await sleep(between(min * 1000, max * 1000))
        }
      }
    }
  } catch (e) {
    if (!(e instanceof StopRun)) throw e
    summary.stoppedBecause = e.message
    log(`Stopped: ${e.message}`)
  } finally {
    await browser?.context.close()
  }

  const counts = summary.outcomes.reduce<Record<string, number>>((acc, o) => ({ ...acc, [o.status]: (acc[o.status] ?? 0) + 1 }), {})
  log(`Done. ${Object.entries(counts).map(([k, v]) => `${k}: ${v}`).join(', ') || 'No new jobs found.'}`)
  return summary
}
