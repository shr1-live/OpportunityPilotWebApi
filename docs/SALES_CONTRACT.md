# Sales pipeline contract (N5)

Status: **review workflow implemented**. Owner-scoped project/tender import, deterministic validation, versioned
bid approval, batch approval, manual tender handoff, provider migrations, and Sales UI are wired. Official Freelancer
discovery/placement remains credential-dependent; no external bid is claimed as placed by this build.

This contract covers the first sales slice after the Candidate phase. The app is a sales assistant: it finds
projects and tenders, prepares proposals and bids, and waits for explicit approval before any external action.

## Product rules

1. Sales data is owner-scoped. A user's projects, tenders, bids, proposals, credentials and activities are never
   visible to another user.
2. Freelancer.com is the first bid-placement integration. Credentials remain server-side; they are never sent to
   the browser or desktop agent.
3. A bid is never placed without approval of the exact current version. Editing amount, delivery time or proposal
   text clears approval and increments the version.
4. Bid amount and delivery time are user-provided inputs. The system must not invent prices, dates, case studies,
   contacts, certifications or capabilities.
5. Tender/RFP submissions remain a manual handoff because digital signatures and portal-specific requirements cannot
   be safely automated in this slice.
6. Email and proposal text can be drafted and approved, but sending remains unavailable until Gmail (M6).
7. Batch approval is a convenience over per-item version approval: one request may approve many items, but each item
   is checked independently and stale, incomplete or blocked items are skipped with a reason.

## Planned concepts

| Concept | Required fields | Lifecycle |
|---|---|---|
| Sales project | owner, external id, source, title, buyer, description, URL, received time, evidence | New → Shortlisted → Bid prepared → Bid approved → Bid placed / Manual handoff / Dismissed |
| Tender/RFP | owner, source, title, buyer, deadline, URL, requirements, evidence | New → Shortlisted → Proposal prepared → Approved → Manual handoff / Dismissed |
| Bid | project, amount, currency, delivery days, proposal text, version, approval hash | Draft → Approved → Placed / Failed; edits return to Draft |
| Proposal draft | project or tender, channel, recipient, subject, body, version, claims, approval hash | Draft → Approved; edits return to Draft |

Every generated claim must cite an evidence id or a confirmed profile claim. Unknown values stay as explicit
`[placeholders]` and block approval until the user resolves them.

## Planned endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/sales/projects` | Owner's projects with status/source filters and paging |
| POST | `/api/v1/sales/projects` | Create a manually entered project while provider ingestion is unavailable |
| GET | `/api/v1/sales/projects/{id}` | Project, evidence, bid and activity details |
| POST | `/api/v1/sales/projects/research` | Queue a configured project/tender source; returns a research job id |
| POST | `/api/v1/sales/projects/{id}/bid` | Create a bid from user-supplied amount, delivery and draft text |
| PUT | `/api/v1/sales/bids/{id}` | Edit the current bid version; clears approval |
| POST | `/api/v1/sales/bids/{id}/approve` | Approve the exact bid version |
| POST | `/api/v1/sales/bids/{id}/place` | Place an approved Freelancer bid using server-side credentials |
| GET | `/api/v1/sales/drafts` | List proposal/email drafts across projects and tenders |
| POST | `/api/v1/sales/drafts/batch-approve` | Approve eligible current versions independently |
| POST | `/api/v1/sales/tenders/{id}/handoff` | Record that the tender was handed to the user for manual submission |

All mutating endpoints use optimistic concurrency (`expectedVersion` or `version`) and return 409 for stale
versions. External calls are idempotent by provider and external id; retries must not place the same bid twice.

## First implementation slice

The domain implements owner-scoped project/bid objects, deterministic validation and versioned bid approval in
`Domain/Sales`; SQL Server and Postgres migrations create their tables. The API accepts manual or explicitly sourced
projects (provider imports require an external id), exposes listing/detail, bid create/edit/exact-version approval,
`POST /api/v1/sales/bids/batch-approve`, and `POST /api/v1/sales/projects/{id}/handoff`. All five campaign modes are
available. Freelancer discovery/placement and Gmail sending remain unavailable until their server-side credentials
and live verification exist.
