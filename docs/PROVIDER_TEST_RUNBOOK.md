# Provider test runbook

Status: active implementation evidence for S14/S15. This file stores no password, cookie, token or session data.

## Test accounts supplied by the owner

- Upwork test workspace: `https://www.upwork.com/nx/find-work/best-matches`
- LinkedIn test profile: `https://www.linkedin.com/in/test1-r-780a82441/?isSelfProfile=true`

These URLs authorize read-only inspection of the signed-in test sessions. They do not authorize copying cookies,
credentials or personal data into OpportunityPilot. Every real proposal submission, Connects expenditure, connection
request, InMail or LinkedIn message requires action-time confirmation from the owner.

## Upwork acceptance path

1. Confirm the account is signed in and the Best Matches page is accessible.
2. Read a bounded sample of visible job cards and one selected job detail without submitting anything.
3. Capture only fields required by the product contract: provider job id/URL, title, summary, skills, budget/rate,
   experience level, client verification/summary, published time and proposal/Connects metadata when visible.
4. Import the selected record as source-linked evidence; deduplicate by provider id and canonical URL.
5. Generate a proposal draft from an approved Sales/Services profile and source evidence.
6. Bind approval to exact job, proposal text, rate, milestones, answers, attachments and visible Connects cost.
7. Open the provider handoff. Stop before the final Upwork submission button and request explicit confirmation.
8. After a confirmed test submission, record provider receipt/result exactly once. An uncertain result remains
   `Unknown` and is never retried blindly.

Until an approved Upwork API/MCP credential is connected, steps 2–7 are an assisted manual handoff. Browser HTML is
not treated as a stable server-side API and OpportunityPilot does not store the Upwork login session.

## LinkedIn acceptance path

1. Confirm the test profile is signed in and its own profile page is accessible.
2. Read the self-profile only to verify the manual handoff target and identity shown to the user.
3. OpportunityPilot may store the user-entered profile URL and prepare connection-note/message drafts.
4. The app opens LinkedIn and copies the approved exact draft for the user.
5. The user sends the request/message. OpportunityPilot records `Sent` only after explicit manual confirmation.

LinkedIn pages are not scraped server-side, cookies are never copied to Render, and the local Candidate application
agent is not reused for Sales connection requests, InMail or messages.

## Evidence log

| Date | Provider | Read-only result | External action | Product follow-up |
|---|---|---|---|---|
| 2026-10-07 | Upwork | Login page reached; the in-app browser is not signed in | None | Assisted import, provider-bound approval, handoff and explicit placement confirmation implemented; sign-in is needed for real visible-field verification |
| 2026-10-07 | LinkedIn | Auth wall reached; the in-app browser is not signed in | None | Existing LinkedIn draft/copy path is being tightened to approved content only; sign-in is needed for profile/deep-link verification |
