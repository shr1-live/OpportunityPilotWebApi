# Replayable Sales and staffing demo

This walkthrough uses only records prefixed `Demo —` and fictional `.example` identities. It never sends a message,
submits a bid, spends marketplace credits, or contacts a real person.

## Run it

1. Open the Sales workspace Overview and select **Create Sales demo**.
2. The application creates Product-profile facts plus Customer, Partner, Investor and Freelance campaigns and queues the
   normal research pipeline. The card reports stored counts; it does not invent progress.
3. When research reaches a terminal state, the UI automatically finishes the demo: one qualified record per mode is
   shortlisted, email/LinkedIn/cover-note drafts and due follow-ups are created, and one exact draft is approved.
4. Open **Companies** to inspect scores and evidence, then **Outreach** to inspect the draft states.
5. Open **Staffing deals**. The fictional deal demonstrates qualification, manual outreach receipt, reply/meeting,
   consented candidate submission, interview notification and completion, client feedback, accepted offer, signed
   contract and placement. Stored lifecycle events drive the staffing KPIs.
6. Select **Reset demo**. After confirmation, only the current owner's `Demo —` records are removed; unrelated data is
   preserved. The workflow can then be replayed.

## Provider boundary

- LinkedIn messages are drafts for copy/open/manual confirmation. OpportunityPilot does not scrape profiles or send them.
- Upwork and Freelancer submissions require approved official API/MCP access and explicit confirmation of the exact bid.
- Email and calendar delivery require provider OAuth. Until configured, the demo records a labelled manual receipt only.
- The signed document reference is private demo metadata, not a real contract or public file.

## Verification

The API integration scenario is `SalesDemoApiTests`. It proves isolation, replay, all four Sales modes, research output,
shortlists, drafts, approval, Won staffing lifecycle, KPIs and reset preservation. It requires Docker/PostgreSQL.
