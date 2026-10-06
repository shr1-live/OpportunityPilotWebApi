import { chromium } from 'playwright'
const [,, out, ...names] = process.argv
const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1440, height: 900 } })
for (const n of names) { await p.goto('file:///D:/OpportunityPilot/opportunitypilot-ui/' + n + '.dc.html'); await p.waitForTimeout(800); await p.screenshot({ path: `${out}/${n}.png`, fullPage: true }) }
await b.close()
