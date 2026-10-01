# Setup

The API refuses to start with a `Setup required: …` message when something below
is missing. Each section says which message it fixes.

## 1. Local development

Prerequisites: .NET 10 SDK, SQL Server LocalDB (ships with Visual Studio), and
Docker if you want to run the integration tests.

```bash
dotnet run --project src/OpportunityPilot.Api
```

`appsettings.Development.json` already sets:

| Setting | Value | Why |
|---|---|---|
| `Database:Provider` | `SqlServer` | LocalDB, no install beyond Visual Studio |
| `Database:MigrateOnStartup` | `true` | Development only; startup throws if set elsewhere |
| `ConnectionStrings:Main` | LocalDB `OpportunityPilot` | |
| `Auth:DevBypass` | `true` | `X-Dev-User: <any name>` signs in as a synthetic user |
| `Cors:AllowedOrigins` | `http://localhost:5173` | the Vite dev server |

Each distinct `X-Dev-User` value maps to a stable, separate owner id, so you can
test isolation between users by changing the name. Dev bypass throws at startup
outside `Development`.

To develop against Postgres instead (e.g. your Supabase database), use user
secrets so nothing lands in git:

```bash
cd src/OpportunityPilot.Api
dotnet user-secrets init
dotnet user-secrets set "Database:Provider" "Postgres"
dotnet user-secrets set "ConnectionStrings:Main" "Host=...;Database=postgres;Username=...;Password=...;SSL Mode=Require"
```

## 2. Configuration reference

Environment variables use `__` for nesting (`ConnectionStrings__Main`).

| Key | Required | Notes |
|---|---|---|
| `ConnectionStrings__Main` | yes | Fixes *ConnectionStrings:Main is not configured* |
| `Database__Provider` | yes | `SqlServer` or `Postgres` (production default `Postgres`) |
| `Auth__SupabaseUrl` | yes outside dev | `https://<ref>.supabase.co`, https only. Fixes *Auth:SupabaseUrl is not configured*. Tokens are validated against `{url}/auth/v1/.well-known/jwks.json` |
| `Auth__Audience` | no | default `authenticated` |
| `Auth__LegacyJwtSecret` | no | only for old Supabase projects still signing with HS256 |
| `Auth__DevBypass` | no | Development only |
| `Cors__AllowedOrigins__0` | yes for a browser | the web app's origin, e.g. `https://app.example.com`; add `__1`, `__2`… for more |
| `Features__GeminiEnabled` | no | `false`. Capability shows *Configured · unverified* when true and a key is present; live calls arrive in M4 |
| `Ai__GeminiApiKey` | no | server-side secret; never returned to the browser |
| `Ai__GeminiModel`, `Ai__MaxCallsPerRun`, `Ai__MaxOutputTokens`, `Ai__AllowPaidUsage` | no | budgets for M4 |
| `Features__GmailEnabled`, `Features__MongoArchiveEnabled` | no | flip capability status from *Disabled* to *Not built yet* |
| `MIGRATE_ON_START` | no | container only: `true` runs `--migrate` as a separate process before the server starts (hosts without a pre-deploy hook) |
| `PORT` | no | set by Render; the app binds `0.0.0.0:$PORT` unless `ASPNETCORE_URLS` is set |

## 3. Migrations

There are two migration sets, one per provider. Add a migration to **both**:

```bash
dotnet ef migrations add <Name> -p src/OpportunityPilot.Infrastructure -s src/OpportunityPilot.Api \
  --context SqlServerAppDbContext -o Persistence/Migrations/SqlServer
dotnet ef migrations add <Name> -p src/OpportunityPilot.Infrastructure -s src/OpportunityPilot.Api \
  --context PostgresAppDbContext -o Persistence/Migrations/Postgres
```

Generation never touches a database (see `DesignTimeFactories.cs`). Everything
lives in the `app` schema, including `__EFMigrationsHistory`.

Applying outside Development is an explicit release step:

```bash
dotnet OpportunityPilot.Api.dll --migrate      # applies pending migrations, then exits
```

## 4. Supabase

1. Create a project. **Project Settings → API**: copy the project URL into
   `Auth__SupabaseUrl` (API) and `VITE_SUPABASE_URL` (web), and the publishable
   (anon) key into `VITE_SUPABASE_PUBLISHABLE_KEY` (web only).
2. **Database → Connect**: use the session pooler connection string for
   `ConnectionStrings__Main` with `Database__Provider=Postgres`.
3. **Authentication → URL configuration**: set the site URL to the web app's
   origin so confirmation emails link back to it.

The browser only uses Supabase for sign-in; it never reads the database directly.

## 5. Deploying to Render

`render.yaml` in this repo is a Render Blueprint for the API (Docker, free plan,
Singapore). The web app repo has its own blueprint. Full step-by-step for both,
in order, is in `DEPLOY.md` at the workspace root; the short version:

1. Render Dashboard → **New → Blueprint** → this repo. Render reads `render.yaml`.
2. When prompted, enter `ConnectionStrings__Main` (Supabase **session pooler**
   string, port 5432 — the direct `db.<ref>.supabase.co` host is IPv6-only and
   unreachable from Render) and `Auth__SupabaseUrl`.
3. Deploy. The container runs `--migrate` first (`MIGRATE_ON_START=true`), then
   starts on `$PORT`. A failed migration stops the new container; the previous
   deploy keeps serving.
4. Check `https://<service>.onrender.com/health/ready` returns `ready`.

`postgres://` URIs and key=value strings both work (`PostgresConnectionString`).
If the web app's URL differs from `https://opportunitypilot-web.onrender.com`,
edit `Cors__AllowedOrigins__0` in the dashboard.

Older Supabase projects that still sign tokens with the shared HS256 secret (the
JWKS endpoint returns no keys) also need `Auth__LegacyJwtSecret` (Project
Settings → JWT keys → legacy secret). Symptom without it: sign-in succeeds, then
the app immediately says the session expired.

Free instances sleep when idle; the web app shows "API · starting" and retries
with backoff rather than reporting a failure.

## 6. Tests

```bash
dotnet test
```

Integration tests start `postgres:17-alpine` with Testcontainers, run the real
API in Development with dev bypass, and exercise two synthetic users. They need
a running Docker daemon; unit tests do not.
