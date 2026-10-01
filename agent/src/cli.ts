import { join } from 'node:path'
import { createInterface } from 'node:readline/promises'
import { report } from './api.ts'
import { openBrowser } from './browser.ts'
import { CONFIG_PATH, DATA_DIR, loadConfig, writeExampleConfig } from './config.ts'
import { linkedIn } from './platforms/linkedin.ts'
import { naukri } from './platforms/naukri.ts'
import type { PlatformAdapter } from './platforms/types.ts'
import { runAgent } from './run.ts'
import { Store } from './store.ts'

const HELP = `OpportunityPilot agent — applies to jobs from your own logged-in browser.

  npm run agent -- init                      create config.json (search, answers, agent key)
  npm run agent -- login <linkedin|naukri>   log in once in the browser window that opens
  npm run agent -- run <linkedin|naukri>     dry run: fill forms, send nothing
        --submit      send real applications (needs "iUnderstandAccountRisk": true)
        --limit N     at most N applications this run
        --headless    no visible window (not recommended)
  npm run agent -- sync                      upload results the web app has not received
  npm run agent -- status                    summary of the local log
`

const RISK = `Unofficial automation: LinkedIn and Naukri terms prohibit automated use and may restrict
your account. Keep the limits in config.json low. The agent stops at any security check.`

function adapterFor(name: string | undefined): PlatformAdapter {
  if (name === 'linkedin') return linkedIn()
  if (name === 'naukri') return naukri()
  throw new Error(`Choose a platform: linkedin or naukri (got "${name ?? ''}").`)
}

const store = () => new Store(join(DATA_DIR, 'applications.jsonl'))

async function main(argv: string[]) {
  const [command, platform, ...rest] = argv
  const flag = (name: string) => rest.includes(name) || platform === name
  const limitArg = rest.indexOf('--limit')
  const limit = limitArg >= 0 ? Number(rest[limitArg + 1]) : undefined

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

    case 'run': {
      const adapter = adapterFor(platform)
      const config = loadConfig()
      const submit = flag('--submit')
      if (submit && !config.iUnderstandAccountRisk) {
        console.error(`${RISK}\n\nTo send real applications, set "iUnderstandAccountRisk": true in config.json, then run again.`)
        process.exitCode = 1
        return
      }
      console.log(`${RISK}\n`)
      if (limit !== undefined && !(limit > 0)) throw new Error('--limit needs a number above 0')
      const summary = await runAgent({ adapter, config, store: store(), submit, limit, headless: flag('--headless') })
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
