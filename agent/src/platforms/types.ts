import type { Page } from 'playwright'
import type { Answers } from '../answers.ts'
import type { SearchConfig } from '../config.ts'
import type { Platform, Status } from '../store.ts'

export interface JobRef {
  externalJobId: string
  url: string
  title: string
  company: string
  location: string | null
  /** Posting text, read by `collect` so the server's research can score it. */
  description?: string
  /** Set when the job came from the user's shortlist; reported back so the opportunity is marked Applied. */
  opportunityId?: string
  /** Exact approved text from OpportunityPilot. Never generated or changed by the agent. */
  coverNote?: string
}

export interface Outcome {
  status: Status
  detail: string
}

export interface ApplyContext {
  /** false = dry run: fill everything, never send. */
  submit: boolean
  answers: Answers
  followCompanies: boolean
  acceptTerms: boolean
  resumePath: string
  coverNote?: string
}

export interface PlatformAdapter {
  platform: Platform
  loginUrl: string
  isLoggedIn(page: Page): Promise<boolean>
  /** Jobs on one results page; empty when there are no more. */
  searchPage(page: Page, search: SearchConfig, keyword: string, pageIndex: number): Promise<JobRef[]>
  /** Opens the job and fills in details the results list did not show. */
  openJob(page: Page, job: JobRef): Promise<JobRef>
  /** The open job's description text (bounded). */
  readDescription(page: Page): Promise<string>
  apply(page: Page, job: JobRef, ctx: ApplyContext): Promise<Outcome>
}
