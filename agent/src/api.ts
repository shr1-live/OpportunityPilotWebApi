import type { ApplicationRecord } from './store.ts'

export type SyncResult = { ok: true } | { ok: false; reason: string }

/** Reports results to the OpportunityPilot API with the user's agent key. Failures never stop a run. */
export async function report(api: { url: string; key: string }, records: ApplicationRecord[]): Promise<SyncResult> {
  if (!api.url || !api.key) return { ok: false, reason: 'api.url or api.key is not set in config.json' }
  try {
    const response = await fetch(`${api.url.replace(/\/$/, '')}/api/v1/applications/report`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Agent-Key': api.key },
      body: JSON.stringify({
        items: records.map(({ synced: _synced, ...r }) => ({
          ...r,
          title: r.title.slice(0, 300),
          company: r.company.slice(0, 300),
          location: r.location?.slice(0, 200) ?? null,
          detail: r.detail?.slice(0, 1000) ?? null,
        })),
      }),
      signal: AbortSignal.timeout(90_000), // a sleeping free host can take a minute to wake
    })
    if (response.ok) return { ok: true }
    if (response.status === 401)
      return { ok: false, reason: 'the API rejected the agent key (revoked, or the demo server restarted). Create a new key on the Applications page.' }
    return { ok: false, reason: `the API answered ${response.status}: ${(await response.text()).slice(0, 300)}` }
  } catch (e) {
    return { ok: false, reason: `the API could not be reached (${(e as Error).message})` }
  }
}
