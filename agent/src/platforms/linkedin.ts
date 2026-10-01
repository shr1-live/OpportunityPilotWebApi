import type { Locator, Page } from 'playwright'
import { captureDebug } from '../browser.ts'
import type { SearchConfig } from '../config.ts'
import { fillStep, validationErrors } from '../form.ts'
import { between, sleep, StopRun } from '../util.ts'
import type { ApplyContext, Outcome, PlatformAdapter } from './types.ts'

const REMOTE = { any: '', onsite: '1', remote: '2', hybrid: '3' } as const
const MAX_STEPS = 15

async function text(locator: Locator): Promise<string> {
  try {
    if ((await locator.count()) === 0) return ''
    return ((await locator.first().innerText({ timeout: 2000 })) ?? '').replace(/\s+/g, ' ').trim()
  } catch {
    return ''
  }
}

/** Ends the run on anything only the user can resolve: a login wall, a security check, the daily limit. */
async function assertUsable(page: Page) {
  const url = page.url()
  if (/\/(checkpoint|challenge)\//.test(url)) throw new StopRun('LinkedIn is asking for a security check. Complete it in the browser, then run again.')
  if (/\/(login|authwall|uas\/login|signup)/.test(url)) throw new StopRun('Not logged in to LinkedIn. Run: npm run agent -- login linkedin')
  if (await page.getByText(/reached the (easy apply )?application limit|limit daily submissions/i).first().isVisible().catch(() => false))
    throw new StopRun('LinkedIn says the daily Easy Apply limit is reached. Try again tomorrow.')
}

export function linkedIn(base = 'https://www.linkedin.com'): PlatformAdapter {
  return {
    platform: 'LinkedIn',
    loginUrl: `${base}/login`,

    async isLoggedIn(page) {
      await page.goto(`${base}/feed/`, { waitUntil: 'domcontentloaded' })
      return !/\/(login|authwall|uas\/login|signup|checkpoint)/.test(page.url())
    },

    async searchPage(page, search: SearchConfig, keyword, pageIndex) {
      const q = new URLSearchParams({
        keywords: keyword,
        location: search.location,
        f_AL: 'true', // Easy Apply only: the rest apply on company sites the agent does not handle
        f_TPR: `r${Math.max(1, search.postedWithinDays) * 86400}`,
        sortBy: 'DD',
        start: String(pageIndex * 25),
      })
      if (REMOTE[search.remote]) q.set('f_WT', REMOTE[search.remote])
      await page.goto(`${base}/jobs/search/?${q}`, { waitUntil: 'domcontentloaded' })
      await assertUsable(page)

      const cards = page.locator('li[data-occludable-job-id]')
      try {
        await cards.first().waitFor({ timeout: 12_000 })
      } catch {
        return []
      }
      // The list renders cards only as they scroll into view; scroll it to the end first.
      for (let i = 0; i < 12; i++) {
        await cards.last().scrollIntoViewIfNeeded().catch(() => {})
        await sleep(250)
      }
      return cards.evaluateAll(
        (els, b) =>
          els
            .map((li) => {
              const id = li.getAttribute('data-occludable-job-id') ?? ''
              const t = (sel: string) => (li.querySelector(sel) as HTMLElement | null)?.innerText.replace(/\s+/g, ' ').trim() ?? ''
              const link = li.querySelector('a[href*="/jobs/view/"], a.job-card-container__link') as HTMLAnchorElement | null
              return {
                externalJobId: id,
                url: `${b}/jobs/view/${id}/`,
                title: (link?.getAttribute('aria-label') ?? t('a.job-card-list__title--link, a.job-card-container__link')).split('\n')[0].trim(),
                company: t('.artdeco-entity-lockup__subtitle, .job-card-container__primary-description'),
                location: t('.artdeco-entity-lockup__caption, .job-card-container__metadata-item') || null,
              }
            })
            .filter((j) => j.externalJobId),
        base,
      )
    },

    async openJob(page, job) {
      await page.goto(job.url, { waitUntil: 'domcontentloaded' })
      await assertUsable(page)
      await page.locator('h1').first().waitFor({ timeout: 12_000 }).catch(() => {})
      const title = await text(page.locator('.job-details-jobs-unified-top-card__job-title h1, h1'))
      const company = await text(page.locator('.job-details-jobs-unified-top-card__company-name, .jobs-unified-top-card__company-name'))
      const location = await text(page.locator('.job-details-jobs-unified-top-card__primary-description-container .tvm__text, .job-details-jobs-unified-top-card__bullet'))
      return { ...job, title: title || job.title, company: company || job.company, location: location || job.location }
    },

    async readDescription(page) {
      // "See more" only toggles CSS; the full text is already in the DOM.
      return (await text(page.locator('.jobs-description__content, #job-details, .jobs-box__html-content'))).slice(0, 20_000)
    },

    async apply(page, job, ctx: ApplyContext): Promise<Outcome> {
      await assertUsable(page)
      if (await page.locator('.artdeco-inline-feedback--success, .jobs-s-apply__application-link').filter({ hasText: /applied/i }).first().isVisible().catch(() => false))
        return { status: 'Skipped', detail: 'Already applied' }

      const easyApply = page.locator('button.jobs-apply-button, button[data-live-test-job-apply-button]').filter({ hasText: /easy apply/i })
      const button = (await easyApply.count()) > 0 ? easyApply.first() : page.getByRole('button', { name: /easy apply/i }).first()
      if (!(await button.isVisible().catch(() => false))) return { status: 'Skipped', detail: 'Not Easy Apply (applies on the company site)' }

      await button.click()
      const dialog = page.locator('.jobs-easy-apply-modal, [data-test-modal-id="easy-apply-modal"]').or(page.getByRole('dialog')).first()
      await dialog.waitFor({ state: 'visible', timeout: 12_000 })
      await assertUsable(page)

      let steps = 0
      for (; steps < MAX_STEPS; steps++) {
        await sleep(between(500, 1000))
        const filled = await fillStep(page, dialog, ctx.answers, { followCompanies: ctx.followCompanies })
        if (filled.unanswered.length) {
          const remaining = await tryResume(dialog, ctx, filled.unanswered)
          if (remaining.length) {
            await discard(page, dialog)
            return { status: 'NeedsManual', detail: `No saved answer for: ${remaining.join('; ')}` }
          }
        }

        const submit = dialog.getByRole('button', { name: /submit application/i })
        if (await submit.isVisible().catch(() => false)) {
          if (!ctx.submit) {
            await discard(page, dialog)
            return { status: 'DryRun', detail: `Filled ${steps + 1} step(s); stopped before Submit (dry run)` }
          }
          await submit.click()
          const sent = page.getByText(/application (was )?sent|you applied|applied to/i).first()
          try {
            await sent.waitFor({ state: 'visible', timeout: 15_000 })
          } catch {
            await captureDebug(page, `linkedin-${job.externalJobId}-no-confirmation`)
            return { status: 'Failed', detail: 'Clicked Submit but saw no confirmation; check the job on LinkedIn' }
          }
          await page.getByRole('button', { name: /dismiss|done|not now/i }).first().click({ timeout: 3000 }).catch(() => {})
          return { status: 'Applied', detail: `Easy Apply, ${steps + 1} step(s)` }
        }

        const next = dialog.getByRole('button', { name: /continue to next step|next|review/i }).first()
        if (!(await next.isVisible().catch(() => false))) {
          await captureDebug(page, `linkedin-${job.externalJobId}-unknown-step`)
          await discard(page, dialog)
          return { status: 'Failed', detail: 'The application form had no Next, Review or Submit button the agent recognises' }
        }
        await next.click()
        await sleep(between(700, 1200))
        let errors = await validationErrors(dialog)
        if (errors.length && (await tryResume(dialog, ctx, ['resume'])).length === 0) {
          await next.click().catch(() => {})
          await sleep(800)
          errors = await validationErrors(dialog)
        }
        if (errors.length) {
          await discard(page, dialog)
          return { status: 'NeedsManual', detail: `LinkedIn rejected an answer: ${errors.join('; ')}` }
        }
      }
      await captureDebug(page, `linkedin-${job.externalJobId}-too-many-steps`)
      await discard(page, dialog)
      return { status: 'Failed', detail: `Gave up after ${MAX_STEPS} form steps` }
    },
  }
}

/** Uploads the configured resume when the only gap is a resume. Returns what is still unanswered. */
async function tryResume(dialog: Locator, ctx: ApplyContext, unanswered: string[]): Promise<string[]> {
  const rest = unanswered.filter((u) => !/resume|cv|upload/i.test(u))
  if (rest.length === unanswered.length || !ctx.resumePath) return unanswered
  const file = dialog.locator('input[type="file"]').first()
  if ((await file.count()) === 0) return unanswered
  await file.setInputFiles(ctx.resumePath)
  await sleep(1500)
  return rest
}

/** Closes the form without sending, confirming LinkedIn's "Discard" prompt. */
async function discard(page: Page, dialog: Locator) {
  await dialog.getByRole('button', { name: /dismiss/i }).first().click({ timeout: 3000 }).catch(() => page.keyboard.press('Escape'))
  await page.getByRole('button', { name: /^discard$/i }).first().click({ timeout: 3000 }).catch(() => {})
  await sleep(500)
}
