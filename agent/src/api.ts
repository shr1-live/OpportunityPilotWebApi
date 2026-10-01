import type { JobRef } from './platforms/types.ts'
import type { ApplicationRecord, Platform } from './store.ts'

export type SyncResult = { ok: true } | { ok: false; reason: string }
type Api = { url: string; key: string }

/** The parts of a campaign's criteria the agent needs to search a platform. */
export interface AgentCampaign {
  id: string
  name: string
  mode: string
  criteria: { keywords: string[]; locations: string[]; workModes: string[] }
}

export interface ShortlistItem {
  opportunityId: string
  campaignId: string
  platform: Platform
  externalId: string
  url: string
  title: string
  organization: string
}

const TIMEOUT = 90_000 // a sleeping free host can take a minute to wake

async function call<T>(api: Api, path: string, init: RequestInit = {}): Promise<{ ok: true; data: T } | { ok: false; reason: string }> {
  if (!api.url || !api.key) return { ok: false, reason: 'api.url or api.key is not set in config.json' }
  try {
    const response = await fetch(`${api.url.replace(/\/$/, '')}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', 'X-Agent-Key': api.key, ...init.headers },
      signal: AbortSignal.timeout(TIMEOUT),
    })
    if (response.status === 401)
      return { ok: false, reason: 'the API rejected the agent key (revoked, or the demo server restarted). Create a new key on the Applications page.' }
    if (response.status === 404) return { ok: false, reason: 'not found — check the campaign id with "npm run agent -- campaigns"' }
    if (!response.ok) return { ok: false, reason: `the API answered ${response.status}: ${(await response.text()).slice(0, 300)}` }
    return { ok: true, data: (response.status === 204 ? undefined : await response.json()) as T }
  } catch (e) {
    return { ok: false, reason: `the API could not be reached (${(e as Error).message})` }
  }
}

/** Reports results with the user's agent key. Failures never stop a run. */
export async function report(api: Api, records: ApplicationRecord[]): Promise<SyncResult> {
  const result = await call(api, '/api/v1/applications/report', {
    method: 'POST',
    body: JSON.stringify({
      items: records.map(({ synced: _synced, opportunityId, ...r }) => ({
        ...r,
        title: r.title.slice(0, 300),
        company: r.company.slice(0, 300),
        location: r.location?.slice(0, 200) ?? null,
        detail: r.detail?.slice(0, 1000) ?? null,
        ...(opportunityId ? { opportunityId } : {}),
      })),
    }),
  })
  return result.ok ? { ok: true } : result
}

export const agentCampaigns = (api: Api) => call<AgentCampaign[]>(api, '/api/v1/agent/campaigns')

export const shortlist = (api: Api, platform: Platform) =>
  call<ShortlistItem[]>(api, `/api/v1/agent/shortlist?platform=${encodeURIComponent(platform)}`)

/** Sends collected postings into a campaign as its research source; the last batch queues research. */
export async function sendPostings(api: Api, campaignId: string, platform: Platform, jobs: JobRef[], queueResearch: boolean) {
  return call<{ accepted: number; sourceId: string; jobId?: string | null }>(api, `/api/v1/agent/campaigns/${encodeURIComponent(campaignId)}/postings`, {
    method: 'POST',
    body: JSON.stringify({
      platform,
      queueResearch,
      items: jobs.map((j) => ({
        externalId: j.externalJobId,
        url: j.url,
        title: j.title.slice(0, 300),
        company: j.company.slice(0, 300),
        location: j.location?.slice(0, 200) ?? null,
        description: j.description?.slice(0, 20_000) ?? null,
      })),
    }),
  })
}
