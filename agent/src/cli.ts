import { join } from 'node:path'
import { createInterface } from 'node:readline/promises'
import { agentCampaigns, report, sendPostings, shortlist } from './api.ts'
import { collectPostings } from './collect.ts'
import { openBrowser } from './browser.ts'
import { CONFIG_PATH, DATA_DIR, loadConfig, writeExampleConfig } from './config.ts'
import { linkedIn } from './platforms/linkedin.ts'
import { naukri } from './platforms/naukri.ts'
import { instahyre } from './platforms/instahyre.ts'
import type { PlatformAdapter } from './platforms/types.ts'
import { applyToShortlist } from './run.ts'
import { Store } from './store.ts'

const HELP = `OpportunityPilot agent — research input and applications from your own logged-in browser.

  npm run agent -- init                      create config.json (answers, limits, agent key)
  npm run agent -- login <linkedin|naukri|instahyre>   log in once in the browser window that opens
  npm run agent -- campaigns                 list your Job campaigns and their ids
  npm run agent -- collect <linkedin|naukri|instahyre> --campaign <id>
                                             search with the campaign's criteria and send the postings
                                             to it for research (nothing is applied to)
  npm run agent -- apply <linkedin|naukri|instahyre>   dry run over the jobs YOU shortlisted after research
        --submit      send real applications (needs "iUnderstandAccountRisk": true)
        --limit N     at most N applications this run
        --headless    no visible window (not recommended)
  npm run agent -- sync                      upload results the web app has not received
  npm run agent -- status                    summary of the local log
`

const RISK = `Unofficial automation: job platforms may prohibit automated use and may restrict
your account. Keep the limits in config.json low. The agent stops at any security check.`

function adapterFor(name: string | undefined): PlatformAdapter {
  if (name === 'linkedin') return linkedIn()
  if (name === 'naukri') return naukri()
  if (name === 'instahyre') return instahyre()
  throw new Error(`Choose a platform: linkedin, naukri or instahyre (got "${name ?? ''}").`)
}

const store = () => new Store(join(DATA_DIR, 'applications.jsonl'))

async function main(argv: string[]) {
  const [command, platform, ...rest] = argv
  const flag = (name: string) => rest.includes(name) || platform === name
  const option = (name: string) => {
    const i = rest.indexOf(name)
    return i >= 0 ? rest[i + 1] : undefined
  }
  const limit = option('--limit') !== undefined ? Number(option('--limit')) : undefined
  if (limit !== undefined && !(limit > 0)) throw new Error('--limit needs a number above 0')

  switch (command) {
    case 'init': {
      const created = writeExampleConfig()
      console.log(
        created
          ? `Created ${CONFIG_PATH}\nEdit it: your search, your answers (salary, notice period, years per skill…) and api.key from the web app's Applications page.`
          : `${CONFIG_PATH} already exists; left unchanged.`,
      )
      return
    }

    case 'login': {
      const adapter = adapterFor(platform)
      const { context, page } = await openBrowser({ headless: false })
      await page.goto(adapter.loginUrl)
      const rl = createInterface({ input: process.stdin, output: process.stdout })
      await rl.question(`Log in to ${adapter.platform} in the browser window (complete any 2-step check). Then press Enter here… `)
      rl.close()
      const ok = await adapter.isLoggedIn(page)
      await context.close()
      console.log(ok ? `Logged in to ${adapter.platform}. The session is saved on this computer only.` : `Still not logged in to ${adapter.platform}. Run login again.`)
      process.exitCode = ok ? 0 : 1
      return
    }

    case 'campaigns': {
      const config = loadConfig()
      const result = await agentCampaigns(config.api)
      if (!result.ok) throw new Error(`Could not list campaigns: ${result.reason}`)
      if (result.data.length === 0) return console.log('No Job campaigns yet. Create one in the web app (Campaigns → New campaign, mode Jobs).')
      for (const c of result.data) console.log(`${c.id}  ${c.name}  — keywords: ${c.criteria.keywords.join(', ') || '(none)'}`)
      return
    }

    case 'collect': {
      const adapter = adapterFor(platform)
      const config = loadConfig()
      const campaignId = option('--campaign')
      if (!campaignId) throw new Error('Which campaign? Add --campaign <id> (see: npm run agent -- campaigns)')
      const campaigns = await agentCampaigns(config.api)
      if (!campaigns.ok) throw new Error(`Could not read the campaign: ${campaigns.reason}`)
      const campaign = campaigns.data.find((c) => c.id === campaignId)
      if (!campaign) throw new Error(`No Job campaign ${campaignId}. See: npm run agent -- campaigns`)

      const { context, page } = await openBrowser({ headless: flag('--headless') })
      try {
        if (!(await adapter.isLoggedIn(page))) throw new Error(`Not logged in to ${adapter.platform}. Run: npm run agent -- login ${platform}`)
        const jobs = await collectPostings({
          adapter,
          page,
          campaign,
          postedWithinDays: config.search.postedWithinDays,
          maxPages: config.limits.maxPages,
          limit: limit ?? config.limits.maxPostingsPerCollect,
          pauseSeconds: config.limits.pauseSeconds,
          skip: (id) => store().isDone(adapter.platform, id),
        })
        if (jobs.length === 0) return console.log('No new postings found.')
        let jobId: string | null | undefined
        for (let i = 0; i < jobs.length; i += 100) {
          const batch = jobs.slice(i, i + 100)
          const sent = await sendPostings(config.api, campaign.id, adapter.platform, batch, i + 100 >= jobs.length)
          if (!sent.ok) throw new Error(`Could not send postings: ${sent.reason}`)
          jobId = sent.data.jobId ?? jobId
        }
        console.log(`Sent ${jobs.length} posting(s) to "${campaign.name}"${jobId ? ' and queued research' : ''}.`)
        console.log(`Next: open the campaign's Opportunities in the web app, shortlist the jobs you want, then run: npm run agent -- apply ${platform}`)
      } finally {
        await context.close()
      }
      return
    }

    case 'apply': {
      const adapter = adapterFor(platform)
      const config = loadConfig()
      const submit = flag('--submit')
      if (submit && !config.iUnderstandAccountRisk) {
        console.error(`${RISK}

To send real applications, set "iUnderstandAccountRisk": true in config.json, then run again.`)
        process.exitCode = 1
        return
      }
      const list = await shortlist(config.api, adapter.platform)
      if (!list.ok) throw new Error(`Could not read your shortlist: ${list.reason}`)
      console.log(`${RISK}
`)
      const jobs = list.data.map((s) => ({
        externalJobId: s.externalId,
        url: s.url,
        title: s.title,
        company: s.organization,
        location: null,
        opportunityId: s.opportunityId,
        coverNote: s.coverNote ?? undefined,
      }))
      const summary = await applyToShortlist({ adapter, config, store: store(), jobs, submit, limit, headless: flag('--headless') })
      if (summary.stoppedBecause) process.exitCode = 2
      return
    }

    case 'sync': {
      const config = loadConfig()
      const s = store()
      const pending = s.all.filter((r) => !r.synced)
      if (pending.length === 0) return console.log('Nothing to sync.')
      for (let i = 0; i < pending.length; i += 100) {
        const batch = pending.slice(i, i + 100)
        const result = await report(config.api, batch)
        if (!result.ok) {
          console.error(`Sync failed: ${result.reason}`)
          process.exitCode = 1
          return
        }
        s.markSynced(batch)
      }
      console.log(`Synced ${pending.length} result(s) to the web app.`)
      return
    }

    case 'status': {
      const s = store()
      const rows = new Map<string, number>()
      for (const r of s.all) rows.set(`${r.platform} ${r.status}`, (rows.get(`${r.platform} ${r.status}`) ?? 0) + 1)
      console.log(`Local log: ${s.all.length} record(s), ${s.all.filter((r) => !r.synced).length} not yet synced, ${s.appliedToday()} applied today.`)
      for (const [k, v] of [...rows].sort()) console.log(`  ${k.padEnd(24)} ${v}`)
      return
    }

    default:
      console.log(HELP)
  }
}

main(process.argv.slice(2)).catch((e: Error) => {
  console.error(e.message)
  process.exitCode = 1
})
