import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Answers } from './answers.ts'

export const AGENT_DIR = resolve(dirname(fileURLToPath(import.meta.url)), '..')
export const CONFIG_PATH = join(AGENT_DIR, 'config.json')
export const DATA_DIR = join(AGENT_DIR, '.data')

export interface SearchConfig {
  /** Each keyword is searched separately. */
  keywords: string[]
  location: string
  remote: 'any' | 'remote' | 'hybrid' | 'onsite'
  postedWithinDays: number
  /** Apply only when the job title contains at least one of these (empty = any title). */
  titleMustIncludeAny: string[]
  titleExclude: string[]
  excludeCompanies: string[]
}

export interface Config {
  api: { url: string; key: string }
  /** Must be true before --submit sends real applications. */
  iUnderstandAccountRisk: boolean
  limits: {
    maxApplicationsPerRun: number
    maxApplicationsPerDay: number
    /** Pause between applications, in seconds [min, max], so the platform is not hammered. */
    pauseSeconds: [number, number]
    maxPages: number
  }
  search: SearchConfig
  profile: { resumePath: string; followCompanies: boolean }
  answers: Answers
}

export const EXAMPLE_CONFIG: Config = {
  api: { url: 'https://opportunitypilotwebapi.onrender.com', key: '' },
  iUnderstandAccountRisk: false,
  limits: { maxApplicationsPerRun: 10, maxApplicationsPerDay: 25, pauseSeconds: [8, 20], maxPages: 3 },
  search: {
    keywords: ['.NET developer', 'Full stack developer'],
    location: 'India',
    remote: 'any',
    postedWithinDays: 7,
    titleMustIncludeAny: ['.net', 'c#', 'full stack', 'fullstack', 'backend'],
    titleExclude: ['intern', 'principal', 'director'],
    excludeCompanies: [],
  },
  profile: { resumePath: '', followCompanies: false },
  answers: {
    fields: [
      { match: ['notice period'], value: '30' },
      { match: ['current ctc', 'current salary', 'current compensation', 'current annual'], value: '1200000' },
      { match: ['expected ctc', 'expected salary', 'expected compensation', 'desired salary'], value: '1800000' },
      { match: ['mobile', 'phone'], value: '9999999999' },
      { match: ['current location', 'city', 'where are you located'], value: 'Bengaluru' },
      { match: ['sponsorship', 'visa'], value: 'No' },
      { match: ['authorized to work', 'authorised to work', 'legally', 'work permit'], value: 'Yes' },
      { match: ['relocate', 'relocation'], value: 'Yes' },
      { match: ['immediate joiner', 'join immediately'], value: 'No' },
      { match: ['hybrid', 'work from office', 'onsite', 'on-site'], value: 'Yes' },
      { match: ['gender', 'race', 'ethnicity', 'veteran', 'disability'], value: 'Prefer not to say' },
      { match: ['linkedin profile', 'linkedin url'], value: 'https://www.linkedin.com/in/your-handle' },
    ],
    skills: { 'c#': 5, '.net core': 5, '.net': 5, 'asp.net': 5, react: 3, typescript: 3, sql: 5, azure: 2 },
    defaults: { yearsOfExperience: 5, yesNo: 'Yes' },
  },
}

export function writeExampleConfig(): boolean {
  if (existsSync(CONFIG_PATH)) return false
  writeFileSync(CONFIG_PATH, JSON.stringify(EXAMPLE_CONFIG, null, 2) + '\n')
  return true
}

/** Reads config.json and reports every problem at once, in words a non-developer can act on. */
export function loadConfig(path = CONFIG_PATH): Config {
  if (!existsSync(path)) throw new Error(`No config found at ${path}. Run: npm run agent -- init`)
  let raw: Partial<Config>
  try {
    raw = JSON.parse(readFileSync(path, 'utf8'))
  } catch (e) {
    throw new Error(`config.json is not valid JSON: ${(e as Error).message}`)
  }
  const c = { ...EXAMPLE_CONFIG, ...raw } as Config
  c.limits = { ...EXAMPLE_CONFIG.limits, ...raw.limits }
  c.search = { ...EXAMPLE_CONFIG.search, ...raw.search }
  c.profile = { ...EXAMPLE_CONFIG.profile, ...raw.profile }
  c.api = { ...EXAMPLE_CONFIG.api, ...raw.api }
  c.answers = {
    fields: raw.answers?.fields ?? [],
    skills: raw.answers?.skills ?? {},
    defaults: { ...EXAMPLE_CONFIG.answers.defaults, ...raw.answers?.defaults },
  }

  const problems: string[] = []
  if (!Array.isArray(c.search.keywords) || c.search.keywords.length === 0) problems.push('search.keywords needs at least one keyword')
  if (!(c.limits.maxApplicationsPerRun > 0)) problems.push('limits.maxApplicationsPerRun must be above 0')
  if (!(c.limits.maxApplicationsPerDay > 0)) problems.push('limits.maxApplicationsPerDay must be above 0')
  if (!Array.isArray(c.limits.pauseSeconds) || c.limits.pauseSeconds.length !== 2) problems.push('limits.pauseSeconds must be [min, max]')
  if (!Array.isArray(c.answers.fields) || c.answers.fields.some((r) => !Array.isArray(r.match) || typeof r.value !== 'string'))
    problems.push('answers.fields must be a list of { "match": ["..."], "value": "..." }')
  if (c.profile.resumePath && !existsSync(c.profile.resumePath)) problems.push(`profile.resumePath does not exist: ${c.profile.resumePath}`)
  if (problems.length) throw new Error(`config.json needs fixing:\n  - ${problems.join('\n  - ')}`)
  return c
}
