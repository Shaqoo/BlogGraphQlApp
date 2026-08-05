# Auth Upgrade — Refresh Tokens & Login Lockout — Design Spec

Date: 2026-08-05
Status: Approved
Scope: Backend (ASP.NET Core 8 + HotChocolate GraphQL + EF Core/MySQL) and one new frontend-facing
doc. Adds one EF migration. Breaking change to the `login`/`verifyEmail` GraphQL response shape.

## Goal

Replace the single 24-hour JWT with a secure two-token scheme (short-lived access token + long-lived,
revocable refresh token) and add a failed-login lockout so accounts can't be brute-forced. Then
document the changes for the frontend team.

1. **Refresh tokens** — DB-backed, revocable, rotated on every use, with reuse detection.
2. **Login lockout** — 5 failed attempts per email -> 10-minute account ban.
3. **Frontend doc** — new `AUTH_FRONTEND.md` describing the new schema, flow, and UX.

## 1. Data model (one EF migration)

### New entity: `RefreshToken` (Entities/RefreshToken.cs)
- `Id` (Guid, PK, BaseEntity style).
- `UserId` (Guid, FK -> User, cascade delete).
- `TokenHash` (string, SHA-256 hex of the raw refresh token — the raw value is never stored).
- `ExpiresAtUtc` (DateTime).
- `CreatedAtUtc` (DateTime).
- `CreatedByIp` (string?, nullable; audit only).
- `RevokedAtUtc` (DateTime?, null = active).
- `ReplacedByTokenId` (Guid?, null = current; points at the token that rotated it).
- Indexes: unique on `TokenHash`, composite on `(UserId, ExpiresAtUtc)`.
- EF config in `Configurations/EfConfigs/RefreshTokenConfiguration.cs`.
- Registered as `DbSet<RefreshToken> RefreshTokens` in `Context/AppDbContext.cs`.

### `User` additions (Entities/User.cs)
- `FailedLoginAttempts` (int, default 0).
- `LockoutEndUtc` (DateTime?, null = not locked).
- Mapped in `Configurations/EfConfigs/UserConfiguration.cs`.

## 2. Token issuance

- Access token: existing `GenerateJwtToken` logic/claims, but lifetime becomes configurable
  `Jwt:AccessTokenMinutes` (default **30**). Validation in `Program.cs` unchanged.
- Refresh token: 256-bit cryptographically random value (e.g. 32 bytes from
  `RandomNumberGenerator`), returned to the client exactly once; only the SHA-256 hash is stored,
  `ExpiresAtUtc = now + Jwt:RefreshTokenDays` (default **30**).
- New `AuthResultDto` (Dtos/): `AccessToken`, `RefreshToken`, `ExpiresIn` (seconds).
- `LoginAsync` and `VerifyEmailAsync` (auto-login after email verification) now return
  `ApiResponse<AuthResultDto>` instead of `ApiResponse<string>`. **Breaking schema change**: the
  GraphQL types for `login` and `verifyEmail` change from `ApiResponseOfString` to
  `ApiResponseOfAuthResult`.

## 3. New GraphQL mutations (AuthMutation)

- `refreshToken(refreshToken: String!): ApiResponse<AuthResult>` — anonymous (no `[Authorize]`).
  Lookup by token hash; fail if unknown, revoked, or expired. On success:
  1. Revoke the presented token (`RevokedAtUtc = now`, set `ReplacedByTokenId`).
  2. Issue a fresh access + refresh pair.
  3. **Reuse detection**: if the presented token was already revoked, revoke the entire session
     family (all tokens chained to it via `ReplacedByTokenId`, plus the original) and reject.
- `logout(refreshToken: String!): ApiResponse<Boolean>` — `[Authorize]`. Revokes the presented
  token only (single-device logout). Idempotent: already-revoked/unknown token still returns success
  so clients can always clear local state.
- `ResetPasswordAsync` (existing) additionally revokes **all** of the user's refresh tokens.

## 4. Login lockout

Config keys: `Jwt:MaxLoginAttempts` (default 5), `Jwt:LoginLockoutMinutes` (default 10).

Flow in `LoginAsync` (and nowhere else):
1. If the email maps to an existing user with `LockoutEndUtc > now`, return the lockout message and
   do **not** attempt password verification.
2. If a `LockoutEndUtc` is in the past, clear it and reset `FailedLoginAttempts` to 0 first.
3. On a bad email or bad password:
   - Existing user: increment `FailedLoginAttempts`; at `MaxLoginAttempts`, set
     `LockoutEndUtc = now + 10 min` and reset `FailedLoginAttempts` to 0. Persist via
     `_unitOfWork.Users.Update` + `CompleteAsync`.
   - Unknown email: track the same counter in the in-memory cache
     (`ICacheService`, key `LoginFailures_{email}`, 10-min TTL). No DB row exists to persist to.
     While that counter is >= `MaxLoginAttempts`, keep rejecting (still generic "Invalid
     credentials.") — the cache entry expires on its own.
4. On success: reset `FailedLoginAttempts` to 0 (and clear the cache key if present), issue tokens.
5. Messages:
   - Bad credentials, not yet locked: `Invalid credentials.` (identical for known/unknown emails).
   - Locked out (known account): `Account is temporarily locked due to too many failed login attempts. Please try again later.`
   - Unknown emails are never given the lockout message (avoids email enumeration).

## 5. Configuration

New keys in `appsettings.json` (placeholders) + `.env` (real values, gitignored):

```
Jwt:AccessTokenMinutes    = 30
Jwt:RefreshTokenDays      = 30
Jwt:MaxLoginAttempts      = 5
Jwt:LoginLockoutMinutes   = 10
```

Read via `_configuration["Jwt:..."]` in `AuthService` (same pattern as `Jwt:Key`).

## 6. Frontend documentation — new `AUTH_FRONTEND.md` (repo root)

Follows the style of `WEB_PUSH_FRONTEND.md` / `FRONTEND_UIUX_GROUP_CHAT.md`. Covers:
- New `login` / `verifyEmail` response shape: `data: { accessToken, refreshToken, expiresIn }`.
- `refreshToken` and `logout` mutations: full query text, variables, and every possible error message.
- The 401 -> refresh -> retry client flow (single in-flight refresh to avoid stampede), and
  re-connecting GraphQL subscriptions with the new access token.
- Where to store tokens (never the refresh token in a place readable by third-party scripts is
  noted; exact storage choice is the frontend's).
- Lockout UX: show the lockout message; disable submit for the remaining lockout if desired.

## 7. Housekeeping

- New `Services/RefreshTokenCleanupService` (BackgroundService, pattern of
  `DailyRoomCleanupService`): periodically (e.g. every 30 min) delete refresh tokens that are
  expired or were revoked more than 30 days ago.
- Lazy purge: `refreshToken` lookup also deletes expired rows for the user opportunistically.

## 8. Explicitly out of scope (future suggestions only)

Google OAuth, TOTP 2FA, password-change endpoint, session/device management UI, admin ban
dashboard, suspicious-login alerts, "remember me" tiers, GDPR export/delete. Not implemented now.

## 9. Testing & verification

No test project exists; verification is live smoke testing against the running app (same approach
as prior hardening/WS work):
1. Build Release with 0 errors.
2. `dotnet ef migrations add` for the new schema; apply to local MySQL.
3. Login -> returns `AuthResult` (access + refresh).
4. `refreshToken` -> new pair; old refresh token rejected; reusing the old (rotated) token triggers
   family revocation.
5. `logout` -> token rejected on later refresh.
6. Lockout: 5 bad passwords for an existing user -> lockout message; 6th correct password still
   rejected while locked; success after the window (verify with a shortened lockout via config or by
   manually clearing `LockoutEndUtc` in DB).
7. Password reset revokes all sessions.
8. Confirm no 429 (rate limiter) interference during tests.
