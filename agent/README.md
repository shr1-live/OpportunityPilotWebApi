# OpportunityPilot apply agent

Applies to jobs on **LinkedIn (Easy Apply)** and **Naukri** from your own logged-in
browser, using answers you saved — no AI, no API keys — and reports every result to
the web app's **Applications** page.

```
You (config.json, log in once)
   └─► agent on your computer ──► LinkedIn / Naukri in a browser window you can watch
                    │
                    └─► OpportunityPilot API (agent key) ──► Applications page
```

> **Unofficial automation.** LinkedIn's and Naukri's terms prohibit automated use;
> they can restrict the account being automated. Keep the limits low. The agent
> does not hide that it is automated and stops at any security check.

## Setup (once)

Requires Node 20.19+ (22 or 24 recommended).

```bash
cd OpportunityPilotWebApi/agent
npm install
npx playwright install chromium
npm run agent -- init                 # creates config.json (git-ignored)
```

Edit `config.json`:

1. **`api.key`** — on the web app's Applications page → *Create agent key* → copy.
2. **`search`** — keywords, location, filters (see below).
3. **`answers`** — your notice period, salaries, phone, city, years per skill, yes/no
   positions. **This is what fills the forms.** Anything not covered is never guessed.
4. **`profile.resumePath`** — optional; uploaded only when a form demands a resume.

Then log in once per site, in the window that opens (2-step checks included):

```bash
npm run agent -- login linkedin
npm run agent -- login naukri
```

The session is kept in `.data/browser-profile/` on this computer. Your password is
never seen or stored by the agent.

## Running

```bash
npm run agent -- run linkedin                 # DRY RUN: fills every form, sends nothing
npm run agent -- run linkedin --submit        # real applications
npm run agent -- run naukri --submit --limit 5
npm run agent -- status                       # what the local log holds
npm run agent -- sync                         # re-upload results the web app is missing
```

`--submit` only works after you set `"iUnderstandAccountRisk": true` in config.json.

**First real use — do it in this order:**
1. `run linkedin` (dry run). Watch the window. Check the Applications page: jobs should
   show *Dry run · not submitted*, *Needs you* or *Skipped* with a reason.
2. For every *Needs you*, add a rule to `answers.fields` covering that question.
3. Then `run linkedin --submit --limit 2`, check those two applications on LinkedIn.
4. Raise the limit gradually.

Naukri sends an application the moment Apply is clicked, so its dry run stops *before*
Apply; its recruiter questionnaire is only exercised with `--submit`.

## What each result means

| Status | Meaning |
|---|---|
| Applied | Submitted and the site confirmed it |
| Dry run | Form filled to the last step, not sent |
| Needs you | A required question your answers don't cover — nothing was sent; apply by hand or add a rule |
| Skipped | Filtered out, already applied, or applies on the company website |
| Failed | Unexpected page; a screenshot + HTML is saved in `.data/debug/` |

A job is never opened twice once it is Applied, Skipped or Needs you (dry runs don't
count). The run stops on a security check, a logged-out session, the site's daily
limit, `limits.maxApplicationsPerDay`, or three failures in a row.

## config.json reference

| Key | Meaning |
|---|---|
| `api.url` / `api.key` | where results go; the key comes from the Applications page |
| `iUnderstandAccountRisk` | must be `true` for `--submit` |
| `limits.maxApplicationsPerRun` / `maxApplicationsPerDay` | caps (applied or dry-run jobs) |
| `limits.pauseSeconds` | `[min, max]` pause between applications |
| `limits.maxPages` | result pages per keyword |
| `search.keywords` | each searched separately |
| `search.location`, `search.remote` (`any`/`remote`/`hybrid`/`onsite`), `search.postedWithinDays` | search filters |
| `search.titleMustIncludeAny`, `search.titleExclude`, `search.excludeCompanies` | filters applied before opening a job |
| `profile.followCompanies` | LinkedIn's "follow company" box (default off) |
| `answers.fields` | `{ "match": ["notice period"], "value": "30" }` — first rule whose phrase appears in the question wins |
| `answers.skills` | `"c#": 6` answers "How many years of experience with C#?" |
| `answers.defaults.yearsOfExperience` | experience questions naming an unknown skill |
| `answers.defaults.yesNo` | Yes/No questions no rule covers; set `""` to treat them as *Needs you* |

## When something breaks

Sites change their pages. If a run reports *Failed* or stops with "the site layout has
probably changed", send the newest files from `.data/debug/` (a `.png` screenshot and
`.html`) — the selectors can be fixed from them.

## Development

```bash
npm run typecheck
npm test        # unit tests + end-to-end runs against a local mock of LinkedIn and Naukri
```

The mock (`test/mock/server.ts`) mirrors the real sites' structure; it proves the
agent's logic, not that the live sites are unchanged. InstaHyre is not supported yet.
