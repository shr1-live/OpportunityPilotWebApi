import type { Locator, Page } from 'playwright'
import { answerFor, pickOption } from '../answers.ts'
import { captureDebug } from '../browser.ts'
import type { SearchConfig } from '../config.ts'
import { between, sleep, slug, StopRun } from '../util.ts'
import type { ApplyContext, Outcome, PlatformAdapter } from './types.ts'

const MAX_QUESTIONS = 20

async function text(locator: Locator): Promise<string> {
  try {
    if ((await locator.count()) === 0) return ''
    return ((await locator.first().innerText({ timeout: 2000 })) ?? '').replace(/\s+/g, ' ').trim()
  } catch {
    return ''
  }
}

function assertUsable(page: Page) {
  if (/\/nlogin\//.test(page.url())) throw new StopRun('Not logged in to Naukri. Run: npm run agent -- login naukri')
}

const SUCCESS = /successfully applied|you have (successfully )?applied|application (has been )?(sent|submitted)|applied to /i

export function naukri(base = 'https://www.naukri.com'): PlatformAdapter {
  return {
    platform: 'Naukri',
    loginUrl: `${base}/nlogin/login`,

    async isLoggedIn(page) {
      await page.goto(`${base}/mnjuser/homepage`, { waitUntil: 'domcontentloaded' })
      return !/\/nlogin\//.test(page.url())
    },

    async searchPage(page, search: SearchConfig, keyword, pageIndex) {
      const path = `${slug(keyword)}-jobs${search.location ? `-in-${slug(search.location)}` : ''}${pageIndex > 0 ? `-${pageIndex + 1}` : ''}`
      const q = new URLSearchParams({ k: keyword, jobAge: String(Math.max(1, search.postedWithinDays)) })
      if (search.location) q.set('l', search.location)
      if (search.remote === 'remote') q.set('wfhType', '2')
      await page.goto(`${base}/${path}?${q}`, { waitUntil: 'domcontentloaded' })
      assertUsable(page)
      const cards = page.locator('.srp-jobtuple-wrapper[data-job-id], [data-job-id].jobTuple')
      try {
        await cards.first().waitFor({ timeout: 12_000 })
      } catch {
        return []
      }
      return cards.evaluateAll((els) =>
        els
          .map((el) => {
            const t = (sel: string) => (el.querySelector(sel) as HTMLElement | null)?.innerText.replace(/\s+/g, ' ').trim() ?? ''
            const link = el.querySelector('a.title') as HTMLAnchorElement | null
            return {
              externalJobId: el.getAttribute('data-job-id') ?? '',
              url: link?.href ?? '',
              title: link?.innerText.replace(/\s+/g, ' ').trim() ?? '',
              company: t('.comp-name, .subTitle'),
              location: t('.locWdth, .loc-wrap .loc, .location') || null,
            }
          })
          .filter((j) => j.externalJobId && j.url),
      )
    },

    async openJob(page, job) {
      await page.goto(job.url, { waitUntil: 'domcontentloaded' })
      assertUsable(page)
      await page.locator('h1').first().waitFor({ timeout: 12_000 }).catch(() => {})
      const title = await text(page.locator('h1'))
      const company = await text(page.locator('[class*="jd-header-comp-name"] a, [class*="jd-header-comp-name"]'))
      const location = await text(page.locator('[class*="jhc__location"] a, [class*="location"] a'))
      return { ...job, title: title || job.title, company: company || job.company, location: location || job.location }
    },

    async readDescription(page) {
      return (await text(page.locator('[class*="job-desc"], .dang-inner-html'))).slice(0, 20_000)
    },

    async apply(page, job, ctx: ApplyContext): Promise<Outcome> {
      assertUsable(page)
      if (await page.locator('#already-applied').or(page.getByRole('button', { name: /^applied$/i })).first().isVisible().catch(() => false))
        return { status: 'Skipped', detail: 'Already applied' }
      if (await page.locator('#company-site-button').or(page.getByRole('button', { name: /apply on company site/i })).first().isVisible().catch(() => false))
        return { status: 'Skipped', detail: 'Applies on the company website' }

      const applyButton = page.locator('#apply-button').or(page.getByRole('button', { name: /^apply$/i })).first()
      if (!(await applyButton.isVisible().catch(() => false))) return { status: 'Skipped', detail: 'No Apply button on the job page' }
      // Naukri submits on the first click when there is no questionnaire, so a dry run stops here.
      if (!ctx.submit) return { status: 'DryRun', detail: 'Stopped before Apply — Naukri sends the application as soon as Apply is clicked (dry run)' }

      await applyButton.click()
      const drawer = page.locator('[class*="chatbot_Drawer"], .chatbot_DrawerContentWrapper').first()
      const success = page.getByText(SUCCESS).first()

      for (let asked = 0; asked <= MAX_QUESTIONS; asked++) {
        const state = await Promise.race([
          success.waitFor({ state: 'visible', timeout: 12_000 }).then(() => 'success' as const),
          drawer.waitFor({ state: 'visible', timeout: 12_000 }).then(() => 'question' as const),
        ]).catch(() => 'timeout' as const)

        if (state === 'success') return { status: 'Applied', detail: asked ? `Applied after answering ${asked} recruiter question(s)` : 'Applied' }
        if (state === 'timeout') {
          await captureDebug(page, `naukri-${job.externalJobId}-no-confirmation`)
          return { status: 'Failed', detail: 'Clicked Apply but saw no confirmation; check the job on Naukri' }
        }

        const before = await drawer.locator('.botMsg').count()
        const question = await text(drawer.locator('.botMsg').last())
        const answered = await answerQuestion(page, drawer, question, ctx)
        if (!answered) return { status: 'NeedsManual', detail: `Recruiter question not covered by your saved answers: "${question}"` }

        // Wait for the bot to move on: a new question, the drawer closing, or the success message.
        for (let i = 0; i < 20; i++) {
          await sleep(400)
          if ((await drawer.locator('.botMsg').count()) > before) break
          if (!(await drawer.isVisible().catch(() => false))) break
          if (await success.isVisible().catch(() => false)) break
        }
        await sleep(between(300, 700))
      }
      await captureDebug(page, `naukri-${job.externalJobId}-too-many-questions`)
      return { status: 'Failed', detail: `Gave up after ${MAX_QUESTIONS} recruiter questions` }
    },
  }
}

/** Answers the chatbot's current question with a radio choice, a chip, or typed text. */
async function answerQuestion(page: Page, drawer: Locator, question: string, ctx: ApplyContext): Promise<boolean> {
  const radios = drawer.locator('input[type="radio"]')
  const chips = drawer.locator('.chatbot_Chip, [class*="chipItem"]')
  const input = drawer.locator('[contenteditable="true"], textarea, input[type="text"]').first()

  if ((await radios.count()) > 0) {
    const options = await radios.evaluateAll((els) =>
      els.map((r) => (r.id ? document.querySelector(`label[for="${CSS.escape(r.id)}"]`)?.textContent?.trim() : '') || (r as HTMLInputElement).value),
    )
    const answer = answerFor({ label: question, kind: 'radio', options }, ctx.answers, ctx.coverNote)
    const pick = answer ? pickOption(options, answer) : undefined
    if (!pick) return false
    const radio = radios.nth(options.indexOf(pick))
    const id = await radio.getAttribute('id')
    if (id && (await drawer.locator(`label[for="${id}"]`).count())) await drawer.locator(`label[for="${id}"]`).first().click()
    else await radio.check({ force: true })
  } else if ((await chips.count()) > 0) {
    const options = (await chips.allInnerTexts()).map((t) => t.trim())
    const answer = answerFor({ label: question, kind: 'radio', options }, ctx.answers, ctx.coverNote)
    const pick = answer ? pickOption(options, answer) : undefined
    if (!pick) return false
    await chips.nth(options.indexOf(pick)).click()
  } else if ((await input.count()) > 0) {
    const answer = answerFor({ label: question, kind: 'text' }, ctx.answers, ctx.coverNote)
    if (answer === undefined) return false
    await input.click()
    await page.keyboard.type(answer, { delay: 30 })
  } else {
    return false
  }

  const send = drawer.locator('.sendMsg, [class*="sendMsg"]').or(drawer.getByRole('button', { name: /^(save|send|submit|next)$/i })).first()
  if (await send.isVisible().catch(() => false)) await send.click()
  return true
}
