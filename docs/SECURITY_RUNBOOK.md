# Security runbook

How to rotate every secret, what the quotas are, and how to check the database is not exposed. Secrets live only in
Render (API) and Vercel (web) environment variables — never in the repo, chat or logs.

## 1. Rotating secrets

| Secret | Where | Rotate by | Effect on users |
|---|---|---|---|
| `ConnectionStrings__Main` (Supabase DB password) | Render | Supabase → Project settings → Database → Reset password; paste the new Session-pooler URI into Render; redeploy | none after the restart |
| Supabase JWT signing keys | Supabase | Supabase → Auth → Signing keys → rotate; the API reads `{Auth__SupabaseUrl}/auth/v1/.well-known/jwks.json`, so nothing to change in Render | signed-in users get a new token on their next refresh |
| `Auth__LegacyJwtSecret` (only old projects) | Render | Move the project to asymmetric keys, then delete this variable | as above |
| `Jsearch__Key` | Render | RapidAPI → Apps → regenerate key; update Render | none |
| `Adzuna__AppId` / `Adzuna__AppKey` | Render | developer.adzuna.com → regenerate; update Render | none |
| `Ai__GeminiApiKey` | Render | Google AI Studio → create new key, delete the old; update Render | none |
| Agent keys (`opk_…`) | per user, DB stores only SHA-256 | User revokes in Applications → Agent setup and creates a new one (scopes: Research, Shortlist, Applications) | that user's agent needs the new key |
| Guest sessions (`opg_…`) | DB stores only SHA-256, 30-day expiry | To end every guest session at once: `DELETE FROM app.guest_sessions;` | every guest is signed out and starts a new private workspace |

After any rotation: `GET /health/ready` must return 200 and `GET /api/v1/capabilities` must show the provider as before.

## 2. Quotas (`src/OpportunityPilot.Api/Hosting/RateLimits.cs`)

Every request: 120 per minute per signed-in owner (else per peer address). On top of that:

| Policy | Endpoint | Limit |
|---|---|---|
| `guest-sign-in` | `POST /api/v1/auth/guest` | 10 per hour per address |
| `imports` | `POST /api/v1/imports/preview` | 30 per hour per owner |
| `research-queue` | `POST /api/v1/campaigns/{id}/research` | 60 per hour per owner |
| `job-board-search` | `/api/v1/jobboards/*`, `/api/v1/indeed/*` | 60 per hour per owner (protects the JSearch monthly quota together with its 6 h cache) |

A rejected request gets 429; the web shows "Too many requests in a short time".

## 3. Agent-key scopes

A key is limited to the agent endpoints (it never satisfies the user policy) and to its scopes: `Research` (read
campaigns, upload postings), `Shortlist` (read the shortlist), `Applications` (report results). Keys made before
scopes existed have all three.

## 4. Security events

`GET /api/v1/security/events` lists the owner's own events: guest session issued, agent key created/revoked, account
data exported/deleted. Details name a key's prefix, never the key. Deleting account data removes the owner's events and
keeps one "AccountDataDeleted" record (type and time only).

## 5. Is schema `app` exposed?

Supabase exposes schemas through its REST API to the `anon` and `authenticated` roles. `app` must not be usable by
either. Check:

```sql
SELECT r.rolname
FROM pg_roles r
WHERE r.rolname IN ('anon', 'authenticated')
  AND has_schema_privilege(r.rolname, 'app', 'USAGE');
```

No rows = not exposed. The same query runs in `GET /api/v1/diagnostics` (field `schemaExposure`). If a role is
listed: Supabase → Settings → API → remove `app` from "Exposed schemas", then `REVOKE USAGE ON SCHEMA app FROM anon,
authenticated;`.
