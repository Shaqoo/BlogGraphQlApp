# Backend Hardening Implementation Plan (Rate Limiting, Security Headers, Secrets Scrub)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add moderate rate limiting, security hardening headers, and purge live secrets from git history in the BlogGraphQlApp backend.

**Architecture:** Three independent workstreams: (1) a `SecurityHeadersMiddleware` that emits hardening headers on API responses, (2) ASP.NET Core's built-in rate limiter configured with a single partitioned global limiter (per-user 300/min, per-IP 100/min, anonymous `/gql` 10/min, webhook and WebSocket exempt), and (3) verification that live secrets never entered git history (appsettings.json is gitignored since the initial commit) after sanitizing `appsettings.json`. No schema/DB/frontend changes.

**Tech Stack:** .NET 8, ASP.NET Core, `System.Threading.RateLimiting` (built-in), HotChocolate GraphQL, git.

## Global Constraints

- Must NOT modify the user's uncommitted MediaType feature files (see `git status` — they are unrelated work). Backup them before any history rewrite.
- Must NOT commit `.env` (it holds real secrets). Never paste real secret values into any committed file — including this plan, code, or commit messages.
- Must NOT block the Daily webhook (`/api/daily/webhook`) or WebSocket subscriptions.
- Static files (`/uploads/**`) must NOT be rate-limited and must NOT receive `Cache-Control: no-store`.
- No CSP header (keeps the Banana Cake Pop tool working in Development). No COEP header.
- HSTS only on HTTPS requests.
- Rejection status for rate limiting: `429` with `Retry-After`.
- `.env` already contains every real secret and is loaded first in `Program.cs` (`DotNetEnv.Env.Load()` before host build), so the app keeps working after `appsettings.json` is sanitized.

---

### Task 1: Sanitize `appsettings.json` (placeholders only) and verify .env drives config

**Files:**
- Modify: `appsettings.json`

**Interfaces:**
- Consumes: none.
- Produces: a sanitized `appsettings.json` (no real secret values). Later tasks (history rewrite) rely on this sanitized file as the source of truth for the scrub.

- [ ] **Step 1: Back up the current (secrets-bearing) appsettings.json to a non-repo path**

```bash
cp appsettings.json /tmp/opencode/appsettings.live.bak.json
echo "backup saved (do not commit this file)"
```

- [ ] **Step 2: Replace every live secret value with a placeholder**

Edit `appsettings.json` so the following values become placeholders (keep JSON valid):

| Section.Key | Replace value with |
|---|---|
| `ConnectionStrings.DefaultConnection` | `server=localhost;port=3306;database=blogapp_gql;user=root;password=CHANGE_ME;` |
| `EmailSettings.Password` | `""` |
| `SpotifySettings.ClientId` | `"your-spotify-client-id"` |
| `SpotifySettings.ClientSecret` | `"your-spotify-client-secret"` |
| `GeminiSettings.ApiKey` | `""` |
| `OpenAI.ApiKey` | `""` |
| `UploadThing.Secret` | `""` |
| `Pinecone.ApiKey` | `""` |

Leave `Storage`, `Jwt` (already a placeholder), `Daily`/`WebPush` (already empty), `AllowedHosts` unchanged.

- [ ] **Step 3: Confirm .env has every scrubbed key (values are there)**

Run: `grep -cE "ConnectionStrings__DefaultConnection|EmailSettings__Password|SpotifySettings__ClientSecret|GeminiSettings__ApiKey|OpenAI__ApiKey|UploadThing__Secret|Pinecone__ApiKey" .env`
Expected: `7` (all present). If any key is missing, add it from `/tmp/opencode/appsettings.live.bak.json` BEFORE continuing.

- [ ] **Step 4: Build and start the app, confirm it still boots from .env**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 &
```

Wait for startup, then:
```bash
curl -s http://192.168.192.1:5000/api/web-push/vapid-key
```
Expected: the real VAPID public key (proves `.env` still overrides the sanitized appsettings).

- [ ] **Step 5: Kill the app**

```bash
# find the dotnet PID for BlogGraphQlApp.dll and kill it
```

- [ ] **Step 6: Confirm the file stays local-only (gitignored)**

`appsettings.json` is listed in `.gitignore` (line 367) and is untracked, so there is **nothing to commit** — the sanitized working copy is the end state.

```bash
git ls-files appsettings.json   # expect: no output (untracked)
git status --short appsettings.json   # expect: no output (ignored)
```

---

### Task 2: Security headers middleware

**Files:**
- Create: `Middleware/SecurityHeadersMiddleware.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `SecurityHeadersMiddleware` (RequestDelegate wrapper) registered in `Program.cs` before `UseCors`.

- [ ] **Step 1: Create `Middleware/SecurityHeadersMiddleware.cs`**

```csharp
using Microsoft.Extensions.Primitives;

namespace BlogGraphQlApp.Middleware
{
    /// <summary>
    /// Emits hardening headers on every response. Static files keep serving without
    /// Cache-Control: no-store, and HSTS is only sent over HTTPS.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var response = context.Response;

            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["X-Frame-Options"] = "DENY";
            response.Headers["Referrer-Policy"] = "no-referrer";
            response.Headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=()";
            response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            response.Headers["X-XSS-Protection"] = "0";

            var isHttps = context.Request.IsHttps ||
                          string.Equals(context.Request.Headers["X-Forwarded-Proto"],
                              "https", StringComparison.OrdinalIgnoreCase);
            if (isHttps)
                response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            var path = context.Request.Path.Value ?? string.Empty;
            var isApi = path.StartsWith("/api/", StringComparison.Ordinal) ||
                        path.Equals("/gql", StringComparison.Ordinal) ||
                        path.StartsWith("/gql/", StringComparison.Ordinal);
            if (isApi && !response.Headers.ContainsKey("Cache-Control"))
                response.Headers["Cache-Control"] = "no-store";

            await _next(context);
        }
    }
}
```

- [ ] **Step 2: Register it in `Program.cs`**

In `Program.cs`, immediately after `var app = builder.Build();` and before `app.UseCors("AllowFrontend");`:

```csharp
app.UseMiddleware<BlogGraphQlApp.Middleware.SecurityHeadersMiddleware>();
```

- [ ] **Step 3: Build and start the app**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 &
```

- [ ] **Step 4: Verify headers on an API endpoint (HTTP)**

```bash
curl -sI http://192.168.192.1:5000/api/web-push/vapid-key
```
Expected (all present): `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy: ...`, `Cross-Origin-Opener-Policy: same-origin`, `X-XSS-Protection: 0`, `Cache-Control: no-store`. **No** `Strict-Transport-Security` (request was HTTP).

- [ ] **Step 5: Verify static files are unaffected**

Upload a small test file to `wwwroot/uploads/profiles/test_header.png`, then:
```bash
curl -sI http://192.168.192.1:5000/uploads/profiles/test_header.png
```
Expected: **no** `Cache-Control: no-store` on the response.

- [ ] **Step 6: Kill the app and remove the test file**

- [ ] **Step 7: Commit**

```bash
git add Middleware/SecurityHeadersMiddleware.cs Program.cs
git commit -m "feat: add security hardening headers middleware"
```

---

### Task 3: Rate limiting

**Files:**
- Modify: `Program.cs` (service registration + middleware order)
- Modify: `Endpoints/DailyWebhookEndpoint.cs` (exempt webhook)

**Interfaces:**
- Consumes: `SecurityHeadersMiddleware` registration from Task 2 (unrelated, no conflict).
- Produces: `GlobalLimiter` partitioned by `user:{id}` / `ip:{addr}`; `429` + `Retry-After` on rejection; webhook disabled.

- [ ] **Step 1: Add the `using` and service registration in `Program.cs`**

Add near the other usings:
```csharp
using System.Security.Claims;
using System.Threading.RateLimiting;
```

After `builder.Services.AddSignalR();` (line ~73) add:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down." }, token);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.WebSockets.IsWebSocketRequest)
            return RateLimitPartition.GetNoLimiter("websocket");

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = !string.IsNullOrEmpty(userId);
        var key = isAuthenticated ? $"user:{userId}" : $"ip:{GetClientIp(context)}";

        var limit = isAuthenticated ? 300
            : context.Request.Path.StartsWithSegments("/gql") ? 10
            : 100;

        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

static string GetClientIp(HttpContext context)
{
    var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwarded))
        return forwarded.Split(',')[0].Trim();
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
```

Note: `context.User` is populated because `UseAuthentication()` runs before `UseRateLimiter()` (verified in Step 2).

- [ ] **Step 2: Insert `UseRateLimiter` in the pipeline**

In `Program.cs` after `app.UseAuthorization();` (line ~327) and before `app.UseHttpsRedirection();` add:

```csharp
app.UseRateLimiter();
```

Final order becomes: `UseCors` → `SecurityHeaders` (Task 2) → `UseWebSockets` → `UseStaticFiles` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `UseHttpsRedirection` → endpoints.

- [ ] **Step 3: Exempt the Daily webhook**

In `Endpoints/DailyWebhookEndpoint.cs`, change the last line from `.AllowAnonymous();` to:

```csharp
}).AllowAnonymous().DisableRateLimiting();
```

- [ ] **Step 4: Build and start the app**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 &
```

- [ ] **Step 5: Verify the anonymous `/gql` limit (10/min) triggers 429**

Run 15 rapid requests (loop is fine; a GraphQL query isn't needed to trip the limiter):
```bash
for i in $(seq 1 15); do curl -s -o /dev/null -w "%{http_code}\n" http://192.168.192.1:5000/gql; done | sort | uniq -c
```
Expected: 10× `301` (HSTS/HTTPS redirect still applies) followed by 5× `429`. The 429 responses must include a `Retry-After` header:
```bash
curl -sI http://192.168.192.1:5000/gql | grep -i retry-after
```

- [ ] **Step 6: Verify the webhook is exempt**

Send 12 rapid `POST /api/daily/webhook` requests (empty body is fine — it returns 200 before parsing matters for rate limiting; if it errors on JSON parse, that is unrelated to the limiter):
```bash
for i in $(seq 1 12); do curl -s -o /dev/null -w "%{http_code}\n" -X POST http://192.168.192.1:5000/api/daily/webhook; done | sort | uniq -c
```
Expected: **no** 429 (all 200, or all identical non-429 codes if the empty body is rejected).

- [ ] **Step 7: Kill the app**

- [ ] **Step 8: Commit**

```bash
git add Program.cs Endpoints/DailyWebhookEndpoint.cs
git commit -m "feat: add partitioned rate limiting, exempt webhook and websockets"
```

---

### Task 4: End-to-end verification (subscriptions still work)

**Files:**
- None (verification only). Requires two test users to exist in the DB (use the standard `register` GraphQL mutation if the DB is empty).

**Interfaces:**
- Consumes: everything from Tasks 1–3 running.

- [ ] **Step 1: Start the app**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 &
```

- [ ] **Step 2: Log in two users, subscribe and publish a group message over WebSocket**

Use the same two-account WS smoke test used previously for `groupMessageSent` (subscribe with the JWT in `connection_init` under `Authorization`, start a `createGroup` + `sendGroupMessage`, assert the event arrives). Confirm the JWT is passed via `connection_init` payload and that the connection is NOT rejected with `AUTH_NOT_AUTHENTICATED` and NOT rate-limited (this proves the WebSocket exemption works).

- [ ] **Step 3: Confirm a logged-in user gets the 300/min bucket, not 10/min**

From the WS client (or a second GraphQL call using the JWT header), issue 20 authenticated `/gql` requests in quick succession.
Expected: none return `429` (authenticated limit is 300/min).

- [ ] **Step 4: Kill the app**

- [ ] **Step 5: Commit any test-script scaffolding used (never .env or tokens)**

```bash
git status --short   # confirm only intended files; the user's MediaType work is untouched
```

---

### Task 5: Verify no secrets ever entered git history (no rewrite required)

**Files:**
- None. Verification only.

**Interfaces:**
- Consumes: the sanitized `appsettings.json` (Task 1) and the `.gitignore` rules.
- Produces: an evidence-backed confirmation that live secrets were never committed, so the destructive rewrite is unnecessary.

> **CORRECTED FINDING:** `appsettings.json` is listed in `.gitignore` (line 367) since the initial commit (`9d8e273`) and has never been tracked (`git ls-files` is empty, no history). There is therefore **no history rewrite to run** — force-pushing rewritten history would be pointless churn and risk. This task verifies that claim and scans every commit for secret fragments using generic prefixes only (never real values).

- [ ] **Step 1: Back up the user's uncommitted MediaType work (defensive only)**

```bash
git diff > /tmp/opencode/mediatype.patch
ls -s /tmp/opencode/mediatype.patch   # confirm non-empty
```

- [ ] **Step 2: Confirm appsettings.json is gitignored and untracked**

```bash
git ls-files appsettings.json; echo "ls-files-exit=$?"
git check-ignore -v appsettings.json
git log --all --oneline -- appsettings.json; echo "log-exit=$?"
```
Expected: `ls-files` prints nothing, `check-ignore` prints the `.gitignore` rule, `log` prints nothing.

- [ ] **Step 3: Scan ALL history for secret fragments (generic markers only)**

```bash
git grep -l -E "sk-proj-|sk_live_|AIzaSy" $(git rev-list --all) 2>/dev/null | sort -u || echo "CLEAN: no secret material in history"
```
Expected: `CLEAN` — if any file is listed, inspect it and scrub only that file (this plan itself was amended to remove such fragments).

- [ ] **Step 4: Confirm no tracked file in the working tree holds real secret values**

```bash
git ls-files | xargs grep -lE "sk-proj-|sk_live_|AIzaSy" 2>/dev/null || echo "CLEAN: no tracked file holds secrets"
```
Expected: `CLEAN`. (`.env` is gitignored and intentionally holds the live values — never commit it.)

- [ ] **Step 5: Document the rotation follow-up (not part of this plan)**

Add a short note to `docs/superpowers/specs/2026-08-03-backend-hardening-design.md` that OpenAI, UploadThing, Gmail SMTP, Gemini, Spotify and Pinecone keys should still be rotated as defense-in-depth, but were **not** exposed through git history. Do not put the key values anywhere.

---

## Self-Review

- **Spec coverage:** secrets scrub → Task 1 (sanitize) + Task 5 (verify never committed — rewrite not needed, appsettings.json is gitignored); rate limiting → Task 3; security headers → Task 2; verification → Task 4; HSTS-only-on-HTTPS, no CSP/COEP, static-file exemption, webhook/WS exemption → covered in Tasks 2–3 and Global Constraints. ✓
- **Placeholder scan:** no TBDs; every step has a command or code block. The only intentional "CHANGE_ME" values are the sanitized placeholders themselves. ✓
- **Type consistency:** middleware class name `SecurityHeadersMiddleware` matches between Task 2 Step 1 and Step 2 registration; `GetClientIp` is defined in the same block where it's used; `options.GlobalLimiter` matches the `PartitionedRateLimiter.Create<HttpContext,string>` return type. ✓
- **Ordering guarantee:** `UseAuthentication` (line ~326) precedes `UseRateLimiter` (inserted after `UseAuthorization`, line ~327), so `context.User` is populated — this is why the per-user 300/min bucket works for authenticated traffic. ✓
