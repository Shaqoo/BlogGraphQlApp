# Backend Hardening — Rate Limiting, Security Headers & Secrets Scrub — Design Spec

Date: 2026-08-03
Status: Approved
Scope: Backend (ASP.NET Core 8 + HotChocolate GraphQL). No frontend changes, no schema/DB changes.

## Goal

Harden the backend against abuse and XSS-ish/clickjacking/mime-sniffing attacks, and purge real
secrets from git history. Three workstreams:

1. **Rate limiting** — protect the API from brute-force / DoS while never blocking the Daily webhook
   or breaking WebSocket subscriptions.
2. **Security headers** — emit modern hardening headers on all API responses; skip CSP deliberately to
   keep the Banana Cake Pop GraphQL tool working in Development.
3. **Secrets scrub** — remove real keys from tracked `appsettings.json`, rely on `.env` (already
   gitignored and loaded via `DotNetEnv`), and rewrite git history to purge them.

## 1. Secrets scrub

> **Corrected finding (verified during implementation):** `appsettings.json` is listed in
> `.gitignore` (line 367) since the initial commit and has never been tracked (`git ls-files` empty,
> no history). **No live secret value exists in any commit**, so the history rewrite is unnecessary.
> The scrub reduces to: sanitize the working `appsettings.json` (defense-in-depth), rely on `.env`,
> and verify no secret fragments in history.

- **`appsettings.json`** (gitignored, untracked): replace every real secret value with an empty
  string or a placeholder. Keys affected: `EmailSettings.Password`, `SpotifySettings.ClientId/ClientSecret`,
  `GeminiSettings.ApiKey`, `OpenAI.ApiKey`, `UploadThing.Secret` (and `Token` if present),
  `Daily`/`WebPush`/`Jwt` are already placeholders/absent in this file — verify.
- **`.env`** (untracked): already contains the real values and is loaded first (Program.cs calls
  `DotNetEnv.Env.Load()` before the host is built), so the app keeps working unchanged after the
  scrub.
- **History rewrite:** NOT required. Verified `git grep` across all commits finds only generic
  prefixes (`sk_live_xxxxx` placeholders in `.env.example`, a `<c>sk_live_...` code comment in
  `Storage/UploadThingStorage.cs`) and the plan's own grep patterns — no real values.
- **Rotation warning (follow-up, not blocking):** the remote is public GitHub
  (`https://github.com/Shaqoo/BlogGraphQlApp.git`). The live keys were **not** exposed via git
  history, but rotate them as defense-in-depth since they may have been shared/committed elsewhere:
  OpenAI, UploadThing, Gmail SMTP password, Gemini, Spotify, Pinecone.
- Working tree safety: this repo currently has uncommitted changes (a MediaType feature) that must
  NOT be committed or touched. The scrub operates on `appsettings.json` + verification only.

## 2. Rate limiting

- **Middleware:** built-in `Microsoft.AspNetCore.RateLimiting` (`System.Threading.RateLimiting`),
  no new package.
- Registration in `Program.cs`:
  - `builder.Services.AddRateLimiter(...)` with named policies.
  - `app.UseRateLimiter()` placed after `UseCors` and before `UseWebSockets`.
- **Policies (moderate):**
  - `global`: fixed window, **100 req/min/IP** (anonymous), **300 req/min/user** when
    authenticated (partition key = JWT `NameIdentifier` when present, else client identity:
    `X-Forwarded-For` first value if present, else remote IP). Applies to all HTTP endpoints.
  - `auth`: fixed window, **10 req/min/IP** for **anonymous** requests to `/gql` (login/register
    are the main unauthenticated operations, and a single GraphQL endpoint can't be limited
    per-mutation). Authenticated traffic uses the global per-user limit instead.
  - Webhook `/api/daily/webhook`: **exempt** via `.DisableRateLimiting()` — Daily must never be
    throttled.
  - Rejection: `StatusCodes.Status429TooManyRequests` + `Retry-After` header.
- **Static files:** `UseStaticFiles` runs **before** `UseRateLimiter`, so uploaded media at
  `/uploads/**` is never throttled (the frontend loads many images).
- **WebSockets:** the `/gql` upgrade request must pass the limiter. Apply the `global` policy but
  ensure `UseWebSockets` sits after `UseRateLimiter` so only the handshake HTTP request is counted,
  not the open connection. Verify subscriptions still work after wiring.
- **GraphQL endpoint:** `/gql` uses the `global` policy. The dev Banana Cake Pop tool is not exempt
  (Development only, acceptable).

## 3. Security headers

- **Middleware:** new small `Middleware/SecurityHeadersMiddleware.cs` (no package), runs early in the
  pipeline (after `UseCors`, before `UseAuthentication`; placed so static files are unaffected).
- Headers emitted on **all non-static responses**:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: no-referrer`
  - `Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`
  - `Cross-Origin-Opener-Policy: same-origin`
  - `X-XSS-Protection: 0` (explicit no-op; the old filter is harmful)
  - `Cache-Control: no-store` on `/api/*` and `/gql` (skip when response already has a Cache-Control,
    e.g. static files)
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains` **only when the request is
    HTTPS** (dev is HTTP — HSTS must never be sent insecurely; respect `X-Forwarded-Proto` for
    proxy deployments).
- **Deliberately omitted:** `Content-Security-Policy` (would break the Banana Cake Pop tool in
  Development) and `Cross-Origin-Embedder-Policy` (would break cross-origin media/iframe loading).

## 4. Verification

1. `dotnet build` succeeds.
2. Start the app; verify:
   - Headers present on `GET /api/web-push/vapid-key` and a GraphQL request.
   - No HSTS header over plain HTTP.
   - Rapid requests to `/gql` (e.g. 120+ in <60s) return `429` with `Retry-After`.
   - `POST /api/daily/webhook` still returns `200` when flooded (exempt).
   - WebSocket subscription still delivers events (incomingCall/groupMessageSent smoke test).
   - Static file `/uploads/...` loads without `Cache-Control: no-store` override and is not
     rate-limited.
3. Confirm `git status` shows only the intended files; the user's uncommitted MediaType work is
   untouched.

## Out of scope

- CSP, COEP, JWT hardening, input validation changes, dependency upgrades, key rotation itself
  (documented as a follow-up), frontend changes.
