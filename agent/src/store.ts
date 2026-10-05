import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'

export type Platform = 'LinkedIn' | 'Naukri'
export type Status = 'Applied' | 'DryRun' | 'NeedsManual' | 'Skipped' | 'Failed'

export interface ApplicationRecord {
  platform: Platform
  externalJobId: string
  jobUrl: string
  title: string
  company: string
  location: string | null
  status: Status
  detail: string | null
  occurredAt: string
  /** The shortlisted opportunity this application belongs to, when it came from research. */
  opportunityId: string | null
  synced: boolean
}

/**
 * The agent's own log (JSON lines). It is the source of truth on this machine: it prevents applying
 * twice and survives the API forgetting everything (demo mode), so `sync` can re-upload it.
 */
export class Store {
  private records: ApplicationRecord[] = []

  constructor(private readonly path: string) {
    if (existsSync(path)) {
      this.records = readFileSync(path, 'utf8')
        .split('\n')
        .filter((l) => l.trim())
        .flatMap((l, i) => {
          // One damaged line must not stop every command; it is reported and skipped.
          try {
            return [JSON.parse(l) as ApplicationRecord]
          } catch {
            console.warn(`Skipping unreadable line ${i + 1} in ${path}`)
            return []
          }
        })
    }
  }

  get all(): readonly ApplicationRecord[] {
    return this.records
  }

  /** Jobs never to open again: applied, or skipped/flagged for a reason a re-run will not change. Dry runs do not count. */
  isDone(platform: Platform, externalJobId: string): boolean {
    return this.records.some(
      (r) => r.platform === platform && r.externalJobId === externalJobId && r.status !== 'DryRun' && r.status !== 'Failed',
    )
  }

  appliedToday(now = new Date()): number {
    const day = now.toDateString()
    return this.records.filter((r) => r.status === 'Applied' && new Date(r.occurredAt).toDateString() === day).length
  }

  add(record: ApplicationRecord) {
    this.records.push(record)
    mkdirSync(dirname(this.path), { recursive: true })
    appendFileSync(this.path, JSON.stringify(record) + '\n')
  }

  markSynced(records: ApplicationRecord[]) {
    for (const r of records) r.synced = true
    writeFileSync(this.path, this.records.map((r) => JSON.stringify(r)).join('\n') + (this.records.length ? '\n' : ''))
  }
}
