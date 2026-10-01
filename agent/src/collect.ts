import type { Page } from 'playwright'
import type { AgentCampaign } from './api.ts'
import { captureDebug } from './browser.ts'
import type { SearchConfig } from './config.ts'
import type { JobRef, PlatformAdapter } from './platforms/types.ts'
import { between, log, sleep } from './util.ts'

/** A platform search built from the campaign: its first non-"Remote" location, and a single work mode if only one is set. */
export function searchFor(campaign: AgentCampaign, postedWithinDays: number): SearchConfig {
  const location = campaign.criteria.locations.find((l) => l.trim() && l.trim().toLowerCase() !== 'remote') ?? ''
  const modes = campaign.criteria.workModes.map((m) => m.toLowerCase())
  const remote = modes.length === 1 && ['remote', 'hybrid', 'onsite'].includes(modes[0]) ? (modes[0] as SearchConfig['remote']) : 'any'
  return { location, remote, postedWithinDays }
}

export interface CollectOptions {
  adapter: PlatformAdapter
  page: Page
  campaign: AgentCampaign
  postedWithinDays: number
  maxPages: number
  limit: number
  pauseSeconds: [number, number]
  /** Jobs already handled locally are not reopened. */
  skip?: (externalJobId: string) => boolean
}

/**
 * Research input, not applications: searches the platform with the campaign's keywords, opens each posting
 * and reads its text so the server can score it with evidence. Nothing is applied to here.
 */
export async function collectPostings(opts: CollectOptions): Promise<JobRef[]> {
  const { adapter, page, campaign } = opts
  if (campaign.criteria.keywords.length === 0) throw new Error(`Campaign "${campaign.name}" has no keywords to search. Add some in the campaign's Filters step.`)
  const search = searchFor(campaign, opts.postedWithinDays)
  const collected = new Map<string, JobRef>()

  outer: for (const keyword of campaign.criteria.keywords) {
    for (let p = 0; p < opts.maxPages; p++) {
      const jobs = await adapter.searchPage(page, search, keyword, p)
      log(`"${keyword}" page ${p + 1}: ${jobs.length} posting(s)`)
      if (jobs.length === 0) break
      for (const listed of jobs) {
        if (collected.size >= opts.limit) break outer
        if (collected.has(listed.externalJobId) || opts.skip?.(listed.externalJobId)) continue
        try {
          const job = await adapter.openJob(page, listed)
          const description = await adapter.readDescription(page)
          collected.set(job.externalJobId, { ...job, description })
          log(`Collected  ${job.title} — ${job.company}${description ? '' : ' (no description text found)'}`)
        } catch (e) {
          const evidence = await captureDebug(page, `${adapter.platform}-${listed.externalJobId}-collect`)
          log(`Could not read ${listed.title || listed.externalJobId}: ${(e as Error).message.split('\n')[0]} (evidence: ${evidence}.png)`)
        }
        const [min, max] = opts.pauseSeconds
        if (max > 0) await sleep(between(min * 300, max * 300)) // reading is lighter than applying
      }
    }
  }
  return [...collected.values()]
}
