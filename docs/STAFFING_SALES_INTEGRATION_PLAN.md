# Staffing-sales integration plan

Status: X1/X2 in progress. This plan is the implementation contract for turning the Sales workspace into a
staffing/recruitment CRM from first lead through signed contract and candidate interview.

## What "local agent" means

The existing Candidate agent is a Node/Playwright program installed on the user's own Windows computer. It opens a
visible browser that is already logged in, reads approved shortlisted job applications, stops at security checks or
unknown questions, and reports the outcome to the API. Vercel and Render do not possess the user's LinkedIn, Naukri or
InstaHyre password/cookies.

That agent is **not** the Sales integration design. It must not be extended to scrape LinkedIn profiles, send connection
requests, send InMail/messages, or start conversations automatically. LinkedIn prohibits third-party scraping and
unauthorised browser automation, including automated contact and messaging activity.

## Required business flow

1. **Define the offer** — sales maintains approved services, roles, rate cards, regions, proof, delivery capacity and
   commercial terms.
2. **Launch a campaign** — select Customer, Partner, Investor or Freelance; define ICP, buyer role, geography, skills,
   budget and hard exclusions.
3. **Discover demand** — ingest permitted public feeds, CSV/user imports, an approved sales-intelligence provider, and
   official Upwork API/MCP results. LinkedIn itself is not scraped.
4. **Qualify and shortlist** — resolve company identity, deduplicate, store source evidence, score visible criteria,
   and let the sales owner reject or shortlist.
5. **Prepare the approach** — choose a contact and channel; generate a connection note, LinkedIn/InMail message, email,
   contact-form response, proposal or marketplace bid from approved company facts and evidence.
6. **Approve the exact action** — approval binds recipient, channel, content, rate card/proposal version, attachments and
   provider cost. Editing any bound field invalidates approval.
7. **Execute or hand off** — official connectors execute only their permitted operation with idempotency and receipt.
   LinkedIn and unsupported portals use a copy/open/manual-send handoff and explicit manual sent confirmation.
8. **Manage the conversation** — inbound or manually recorded messages belong to one account/contact/deal thread. Sales
   records reply intent, next action, due time, owner and private notes.
9. **Schedule discussion** — create an approved Google/Outlook calendar event or share a scheduling link; keep invitee,
   time zone, conference link, reschedule and cancellation status.
10. **Select company candidates** — enrol internal candidates with consent and field-level sharing choices. Sales chooses
    the exact resume version, skills, proof, rate, availability and documents for one client submission.
11. **Submit candidates** — freeze an immutable submission package, deliver through an official email/provider connector
    or manual handoff, store the receipt, and notify only the selected candidates with approved information.
12. **Run interviews** — track rounds, panel, schedule, candidate instructions, attendance and client/candidate feedback.
13. **Close the commercial outcome** — track selection/rejection, candidate offer, commercial offer, start date,
    placement value, contract version and signature status.
14. **Measure the funnel** — derive every KPI from stored stage/activity events: lead, contacted, replied, qualified,
    meeting, submission, interview, offer, placement and contract.

## Provider integration boundary

| Provider or activity | Discovery/read | Draft | Execute | Required control |
|---|---|---|---|---|
| LinkedIn company/contact research | Approved LinkedIn partner capability or separate licensed sales-intelligence provider; otherwise user-entered facts/URLs only | Connection note, InMail and message drafts | Manual copy/open/send and manual status confirmation unless an approved LinkedIn API product explicitly grants the operation | No scraping, browser bot, cookie capture or silent sending |
| Upwork | Official Upwork MCP or API within the account's approved scope | Proposal and bid details | MCP/API submission only after the user explicitly confirms that exact proposal | Store Connects cost, idempotency key and provider result; never retry an uncertain submission blindly |
| Freelancer.com | Official API if approved; otherwise import/manual project | Proposal and bid | Approved API scope or manual handoff | Exact-version approval and provider receipt |
| Naukri/other staffing portals | Official recruiter/feed/API contract where available; otherwise import/manual entry | Message/submission package | Approved connector or manual handoff | Do not reuse candidate job-apply automation for recruiter/sales outreach |
| Gmail | Gmail OAuth API | Internal draft; optional provider draft | Send only an approved exact version | Recipient verification, suppression, idempotency and Gmail message id |
| Microsoft email | Microsoft Graph delegated OAuth | Internal draft | Send only an approved exact version | Same controls as Gmail |
| Google/Outlook calendar | Calendar OAuth API | Meeting proposal | Create/update/cancel after approval | Time zone, invitees, provider event id and change audit |
| Contact forms/tender portals | Permitted public URL plus user-supplied data | Response/proposal | Manual handoff unless an official API exists | Never claim submitted without a receipt/manual confirmation |
| E-signature | DocuSign/Adobe Sign or equivalent official API | Contract envelope preparation | Send after approval | Envelope id, signer list, document hash and final signed status |

## Source-of-truth model

- **Account** — company identity, domain, location, source references and qualification facts.
- **Contact** — person/role and permitted contact paths; no invented email, phone or LinkedIn facts.
- **Deal** — the commercial opportunity, owner, source, mode, value/currency, stage, next action and current outcome.
- **Deal activity** — immutable stage/action audit including safe human-entered notes and provider/manual receipts.
- **Conversation** — messages and call notes linked to account/contact/deal; provider data stays attributable.
- **Rate card / proposal** — versioned commercial terms whose approval is invalidated by an edit.
- **Candidate** — internal talent identity, consent and sharing preferences.
- **Submission** — immutable candidate package for one deal/client and the exact facts/documents approved for sharing.
- **Interview** — one round with schedule, participants, candidate-visible instructions and feedback visibility.
- **Offer / contract** — candidate/commercial outcome, document version, signature state and placement value.

The first implementation slice (X2) introduces owner-scoped accounts, contacts, deals and immutable deal-stage
activities. It intentionally performs no external sending. Provider execution is layered on only after this source of
truth exists.

## Deal stages

`New → Qualified → Shortlisted → OutreachApproved → Contacted → Replied → MeetingScheduled → RequirementConfirmed →`
`CandidatesSubmitted → Interviewing → Offer → Contracting → Won`

`Disqualified`, `Lost` and `OnHold` are explicit terminal/paused outcomes. The API enforces allowed forward transitions;
it never turns a draft/copy action into `Contacted` without a provider receipt or the user's manual confirmation.

## Candidate privacy and communication

- Candidate data is never automatically shared because the person was enrolled.
- Every submission specifies the candidate, resume/document version and selected shareable fields.
- Private recruiter notes and client-only commercial terms are never candidate-visible.
- Sales chooses which interview details and feedback are shared with each candidate.
- Candidate notifications use approved templates and record delivery/failure; the UI never claims delivery without a
  provider receipt.
- Export, deletion, retention and access audit cover candidate documents and submissions.

## Reliability and safety requirements

- Owner scope on every read/write; organization roles are added before multi-user sharing.
- Exact-version approval for every externally visible action.
- Idempotency keys for sends, bids, calendar events and signature envelopes.
- Provider receipt or explicit manual confirmation before advancing an external-action stage.
- Suppression, recipient verification, quota and consent checks at draft approval and execution.
- Bounded retry; an uncertain provider response is `Unknown`, never silently retried or reported successful.
- Secrets/tokens remain server-side encrypted provider configuration and never appear in logs or API responses.
- Correlation id and immutable activity for every transition.

## Delivery order

1. X2 accounts/contacts/deals/activity foundation.
2. X4 rate cards and exact proposal versions.
3. X3 compliant discovery/import connectors.
4. X5 provider-neutral approved-action gateway plus manual handoffs.
5. X6 conversation and follow-up lifecycle.
6. X7 calendar scheduling.
7. X8/X9 candidate consent and submission packages.
8. X10/X11 interviews, notifications and feedback.
9. X12 offers, contracts and signatures.
10. X13 roles/ownership, X14 KPIs, then X15 deployed demonstration.

## Sources and coverage

- LinkedIn automated activity policy: https://www.linkedin.com/help/linkedin/answer/a1340567/automated-activity-on-linkedin?lang=en
- LinkedIn User Agreement: https://www.linkedin.com/legal/user-agreement
- Upwork MCP guidance: https://support.upwork.com/hc/en-us/articles/55446516654611-How-to-use-Upwork-with-AI-agents-through-MCP
- Upwork automation guidance: https://support.upwork.com/hc/en-us/articles/43342677368467-Use-bots-and-other-automation-properly
- Current repository code and `TASKS.md` define existing product coverage.

No sales-intelligence, CRM, calendar, email or e-signature provider is connected in this workspace yet. Coverage is
therefore an architecture and implementation contract, not proof of provider availability. Do not run any
credit-consuming search, export, enrichment, or personal-contact access without the user's prior explicit approval.
