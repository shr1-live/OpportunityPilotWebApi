import { mkdtempSync, rmSync } from 'node:fs'
import { createServer } from 'node:http'
import type { AddressInfo } from 'node:net'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { chromium, type Browser, type Page } from 'playwright'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { newPreparedPage } from '../src/browser.ts'
import { EXAMPLE_CONFIG, type Config } from '../src/config.ts'
import { linkedIn } from '../src/platforms/linkedin.ts'
import { naukri } from '../src/platforms/naukri.ts'
import { runAgent } from '../src/run.ts'
import { Store } from '../src/store.ts'
import { startMock } from './mock/server.ts'

let mock: Awaited<ReturnType<typeof startMock>>
let browser: Browser
let page: Page
let dir: string

const config = (api = { url: '', key: '' }): Config => ({
  ...EXAMPLE_CONFIG,
  api,
  limits: { maxApplicationsPerRun: 10, maxApplicationsPerDay: 25, pauseSeconds: [0, 0], maxPages: 1 },
  search: { ...EXAMPLE_CONFIG.search, keywords: ['dotnet'], titleMustIncludeAny: ['.net', 'c#'], titleExclude: ['intern'] },
  answers: { ...EXAMPLE_CONFIG.answers, skills: { 'c#': 6 } },
})

const byJob = (outcomes: { job: { externalJobId: string }; status: string; detail: string }[]) =>
  Object.fromEntries(outcomes.map((o) => [o.job.externalJobId, o]))

beforeAll(async () => {
  mock = await startMock()
  browser = await chromium.launch()
})
afterAll(async () => {
  await browser?.close()
  await mock?.close()
})
beforeEach(async () => {
  mock.submissions.length = 0
  dir = mkdtempSync(join(tmpdir(), 'op-agent-'))
  page = await newPreparedPage(browser)
  page.setDefaultTimeout(5000)
  return async () => {
    await page.context().close()
    rmSync(dir, { recursive: true, force: true })
  }
})

describe('LinkedIn', () => {
  const adapter = () => linkedIn(`${mock.origin}/li`)

  it('dry run fills every step but sends nothing, and explains each skip', async () => {
    const store = new Store(join(dir, 'log.jsonl'))
    const { outcomes } = await runAgent({ adapter: adapter(), config: config(), store, submit: false, page })
    const r = byJob(outcomes)

    expect(r['1001'].status).toBe('DryRun')
    expect(r['1002'].status).toBe('NeedsManual')
    expect(r['1002'].detail).toContain('favourite programming joke')
    expect(r['1003']).toMatchObject({ status: 'Skipped', detail: 'Already applied' })
    expect(r['1004'].detail).toMatch(/not easy apply/i)
    expect(r['1005'].detail).toMatch(/excluded word "intern"/)
    expect(mock.submissions).toHaveLength(0)
  })

  it('submit mode sends exactly the saved answers and never applies twice', async () => {
    const store = new Store(join(dir, 'log.jsonl'))
    const first = await runAgent({ adapter: adapter(), config: config(), store, submit: true, page })
    expect(byJob(first.outcomes)['1001'].status).toBe('Applied')
    expect(mock.submissions).toEqual([
      {
        platform: 'LinkedIn',
        job: '1001',
        values: {
          email: 'me@example.com', // prefilled value left alone
          phone: '9999999999',
          city: 'Bengaluru, Karnataka, India', // picked from the typeahead, not just typed
          q1: '6',
          q2: 'Yes',
          spons: 'No',
          follow: false,
        },
      },
    ])

    const second = await runAgent({ adapter: adapter(), config: config(), store, submit: true, page })
    expect(second.outcomes.map((o) => o.job.externalJobId)).not.toContain('1001')
    expect(mock.submissions).toHaveLength(1)
  })

  it('reports each result to the API with the agent key', async () => {
    const received: { key: string | undefined; body: { items: { externalJobId: string; status: string }[] } }[] = []
    const api = createServer((req, res) => {
      let body = ''
      req.on('data', (c) => (body += c))
      req.on('end', () => {
        if (req.url === '/api/v1/applications/report') received.push({ key: req.headers['x-agent-key'] as string, body: JSON.parse(body) })
        res.writeHead(200, { 'Content-Type': 'application/json' }).end('{"accepted":1}')
      })
    })
    await new Promise<void>((resolve) => api.listen(0, '127.0.0.1', resolve))
    const url = `http://127.0.0.1:${(api.address() as AddressInfo).port}`
    try {
      const store = new Store(join(dir, 'log.jsonl'))
      await runAgent({ adapter: adapter(), config: config({ url, key: 'opk_test' }), store, submit: true, page })
      expect(received.every((r) => r.key === 'opk_test')).toBe(true)
      expect(received.flatMap((r) => r.body.items).find((i) => i.externalJobId === '1001')?.status).toBe('Applied')
      expect(store.all.every((r) => r.synced)).toBe(true)
    } finally {
      api.close()
    }
  })

  it('stops the whole run when LinkedIn shows the login wall', async () => {
    const store = new Store(join(dir, 'log.jsonl'))
    const summary = await runAgent({ adapter: linkedIn(`${mock.origin}/wall`), config: config(), store, submit: true, page })
    expect(summary.outcomes).toHaveLength(0)
    expect(summary.stoppedBecause).toMatch(/not logged in to linkedin/i)
  })
})

describe('Naukri', () => {
  const adapter = () => naukri(`${mock.origin}/nk`)

  it('dry run stops before Apply because Naukri submits on click', async () => {
    const store = new Store(join(dir, 'log.jsonl'))
    const { outcomes } = await runAgent({ adapter: adapter(), config: config(), store, submit: false, page })
    const r = byJob(outcomes)
    expect(r['2001'].status).toBe('DryRun')
    expect(r['2003']).toMatchObject({ status: 'Skipped', detail: 'Already applied' })
    expect(r['2004'].detail).toMatch(/company website/)
    expect(mock.submissions).toHaveLength(0)
  })

  it('submit mode answers the recruiter chatbot from saved answers and flags what it cannot answer', async () => {
    const store = new Store(join(dir, 'log.jsonl'))
    const { outcomes } = await runAgent({ adapter: adapter(), config: config(), store, submit: true, page })
    const r = byJob(outcomes)
    expect(r['2001'].status).toBe('Applied')
    expect(r['2002']).toMatchObject({ status: 'Applied' })
    expect(r['2005'].status).toBe('NeedsManual')
    expect(r['2005'].detail).toContain('favourite project')
    expect(mock.submissions.find((s) => s.job === '2002')?.values).toEqual({ q0: 'Yes', q1: '30' })
    expect(mock.submissions.map((s) => s.job).sort()).toEqual(['2001', '2002'])
  })
})
