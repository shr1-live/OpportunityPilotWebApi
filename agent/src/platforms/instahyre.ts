import type { Locator, Page } from 'playwright'
import { captureDebug } from '../browser.ts'
import type { SearchConfig } from '../config.ts'
import { StopRun } from '../util.ts'
import type { ApplyContext, Outcome, PlatformAdapter } from './types.ts'

async function text(locator: Locator): Promise<string> {
  try { return ((await locator.first().innerText({ timeout: 2000 })) ?? '').replace(/\s+/g, ' ').trim() } catch { return '' }
}

function assertUsable(page: Page) {
  const url = page.url()
  if (/login|signin|auth/i.test(url)) throw new StopRun('Not logged in to InstaHyre. Run: npm run agent -- login instahyre')
  if (/captcha|challenge|verify/i.test(url)) throw new StopRun('InstaHyre is asking for a security check. Complete it in the browser, then run again.')
}

/** Candidate opportunities are personalised by InstaHyre; keyword/location arguments are retained for the common adapter contract. */
export function instahyre(base = 'https://www.instahyre.com'): PlatformAdapter {
  return {
    platform: 'Instahyre',
    loginUrl: `${base}/login/`,
    async isLoggedIn(page) {
      await page.goto(`${base}/candidate/opportunities/`, { waitUntil: 'domcontentloaded' })
      return !/login|signin|auth/i.test(page.url())
    },
    async searchPage(page, _search: SearchConfig, _keyword, pageIndex) {
      if (pageIndex > 0) return []
      await page.goto(`${base}/candidate/opportunities/`, { waitUntil: 'domcontentloaded' })
      assertUsable(page)
      const cards = page.locator('[data-job-id], .opportunity-card, .job-card')
      await cards.first().waitFor({ timeout: 12_000 }).catch(() => {})
      return cards.evaluateAll((els, origin) => els.map((el, index) => {
        const link = el.querySelector('a[href*="opportunity"], a[href*="job"]') as HTMLAnchorElement | null
        const value = (selector: string) => (el.querySelector(selector) as HTMLElement | null)?.innerText.replace(/\s+/g, ' ').trim() ?? ''
        const href = link?.href || ''
        const fromUrl = href.match(/(?:opportunity|job)[/-]([a-z0-9-]+)/i)?.[1]
        return { externalJobId: el.getAttribute('data-job-id') || fromUrl || `visible-${index}`, url: href || origin,
          title: value('h2, h3, .job-title, .title'), company: value('.company-name, .company, [data-company]'), location: value('.location, [data-location]') || null }
      }).filter((job) => job.url !== origin && job.title), base)
    },
    async openJob(page, job) {
      await page.goto(job.url, { waitUntil: 'domcontentloaded' }); assertUsable(page)
      return { ...job, title: await text(page.locator('h1, .job-title')) || job.title,
        company: await text(page.locator('.company-name, .company')) || job.company,
        location: await text(page.locator('.location, [data-location]')) || job.location }
    },
    async readDescription(page) { return (await text(page.locator('.job-description, .description, [data-job-description]'))).slice(0, 20_000) },
    async apply(page, job, ctx: ApplyContext): Promise<Outcome> {
      assertUsable(page)
      if (await page.getByText(/already applied|application sent/i).first().isVisible().catch(() => false)) return { status: 'Skipped', detail: 'Already applied' }
      const apply = page.getByRole('button', { name: /^apply$/i }).or(page.getByRole('link', { name: /^apply$/i })).first()
      if (!(await apply.isVisible().catch(() => false))) return { status: 'Skipped', detail: 'No Apply control on this opportunity' }
      if (!ctx.submit) return { status: 'DryRun', detail: 'Stopped before Apply — InstaHyre may submit immediately (dry run)' }
      await apply.click()
      const confirmed = page.getByText(/successfully applied|application sent|you applied/i).first()
      if (await confirmed.isVisible({ timeout: 12_000 }).catch(() => false)) return { status: 'Applied', detail: 'Applied on InstaHyre' }
      await captureDebug(page, `instahyre-${job.externalJobId}-no-confirmation`)
      return { status: 'NeedsManual', detail: 'Apply opened a step the agent cannot safely answer; finish it in the browser' }
    },
  }
}
