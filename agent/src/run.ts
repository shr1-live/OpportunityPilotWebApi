import type { Page } from 'playwright'
import { report } from './api.ts'
import { captureDebug, openBrowser } from './browser.ts'
import type { Config } from './config.ts'
import type { JobRef, Outcome, PlatformAdapter } from './platforms/types.ts'
import type { ApplicationRecord, Store } from './store.ts'
import { between, log, sleep, StopRun } from './util.ts'

export interface ApplyOptions {
  adapter: PlatformAdapter
  config: Config
  store: Store
  /** The user's shortlist: research scored these and the user chose them. Nothing else is applied to. */
  jobs: JobRef[]
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

export async function applyToShortlist(opts: ApplyOptions): Promise<RunSummary> {
  const { adapter, config, store } = opts
  const summary: RunSummary = { outcomes: [], stoppedBecause: null }
  const remainingToday = config.limits.maxApplicationsPerDay - store.appliedToday()
  const limit = Math.min(opts.limit ?? config.limits.maxApplicationsPerRun, opts.submit ? remainingToday : Infinity)
  if (limit <= 0) {
    summary.stoppedBecause = `Daily limit of ${config.limits.maxApplicationsPerDay} applications already reached.`
    log(summary.stoppedBecause)
    return summary
  }
  const pending = opts.jobs.filter((j) => !store.isDone(adapter.platform, j.externalJobId))
  if (pending.length === 0) {
    log(`Nothing to apply to: no shortlisted ${adapter.platform} jobs that are not already handled.`)
    return summary
  }

  const browser = opts.page ? null : await openBrowser({ headless: opts.headless, profileDir: opts.profileDir })
  const page = opts.page ?? browser!.page
  let syncWorking = Boolean(config.api.url && config.api.key)
  let attempts = 0
  let consecutiveFailures = 0

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
      opportunityId: job.opportunityId ?? null,
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
    log(`${opts.submit ? 'SUBMIT mode — real applications will be sent' : 'DRY RUN — forms are filled but nothing is sent'}. ${pending.length} shortlisted job(s); limit this run: ${limit}.`)

    for (const listed of pending) {
      if (attempts >= limit) break
      let job = listed
      try {
        job = { ...(await adapter.openJob(page, listed)), opportunityId: listed.opportunityId, coverNote: listed.coverNote }
        const outcome = await adapter.apply(page, job, {
          submit: opts.submit,
          answers: config.answers,
          followCompanies: config.profile.followCompanies,
          acceptTerms: config.profile.acceptTermsCheckboxes,
          resumePath: config.profile.resumePath,
          coverNote: listed.coverNote,
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
      if (consecutiveFailures >= 3) throw new StopRun('Three applications in a row failed — the site layout has probably changed. Evidence is in .data/debug.')

      const [min, max] = config.limits.pauseSeconds
      if (max > 0) await sleep(between(min * 1000, max * 1000))
    }
  } catch (e) {
    if (!(e instanceof StopRun)) throw e
    summary.stoppedBecause = e.message
    log(`Stopped: ${e.message}`)
  } finally {
    await browser?.context.close()
  }

  const counts = summary.outcomes.reduce<Record<string, number>>((acc, o) => ({ ...acc, [o.status]: (acc[o.status] ?? 0) + 1 }), {})
  log(`Done. ${Object.entries(counts).map(([k, v]) => `${k}: ${v}`).join(', ') || 'Nothing applied.'}`)
  return summary
}
