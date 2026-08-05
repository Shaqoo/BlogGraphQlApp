# Login Lockout Countdown — Design

> **Date:** 2026-08-05
> **Status:** Approved
> **Builds on:** `2026-08-05-auth-refresh-tokens-lockout-design.md` and its implementation (now merged into `main` at `0f716d1`).

## Goal

When a user attempts to log in while their account is locked (5 failed attempts → 10-minute ban),
the `login` mutation must return the **time remaining** in the lockout window so the frontend can
render a live countdown. The response shape and the frontend guidance are documented in
`AUTH_FRONTEND.md`.

## Current behavior

`AuthService.LoginAsync` (`Services/Implementations/AuthService.cs:125-128`) returns
`ApiResponse<AuthResultDto>.Fail(LockoutMessage)` — a static message and `data: null`, with no
time information.

The shared response envelope (`Dtos/ApiResponse.cs`) has `Succeeded`, `Data`, `Message`, `Errors`
(all `private set`).

## Design

### 1. Response envelope — two new nullable fields

`Dtos/ApiResponse.cs`, add to `ApiResponse<T>`:

- `public int? LockoutRemainingSeconds { get; set; }` — whole seconds until the account unlocks
  (rounded **up**).
- `public DateTime? LockoutEndsAtUtc { get; set; }` — the UTC instant the lockout expires
  (ISO-8601 in the GraphQL response).

Both are `null` on every non-lockout response. Because they live on the shared envelope, they
appear on every `ApiResponse*` GraphQL type as optional fields — additive and non-breaking.

### 2. Lockout failure factory

`Dtos/ApiResponse.cs`, add a static factory (required because the properties are `private set`):

```csharp
public static ApiResponse<T> FailLocked(string message, int lockoutRemainingSeconds, DateTime lockoutEndsAtUtc)
{
    return new ApiResponse<T>
    {
        Succeeded = false,
        Message = message,
        Errors = [message],
        LockoutRemainingSeconds = lockoutRemainingSeconds,
        LockoutEndsAtUtc = lockoutEndsAtUtc
    };
}
```

### 3. `AuthService.LoginAsync` lockout path

Replace the current `return ApiResponse<AuthResultDto>.Fail(LockoutMessage);` (line 127) with:

```csharp
var remaining = (int)Math.Ceiling((lockoutEnd - now).TotalSeconds);
var message = $"Account is temporarily locked due to too many failed login attempts. Please try again in {FormatRemaining(remaining)}.";
return ApiResponse<AuthResultDto>.FailLocked(message, remaining, lockoutEnd);
```

Add a small private helper that formats `remaining` seconds as human text:

- `>= 60` seconds → `"{m}m {s}s"` (e.g. `9m 32s`), omitting `0s` when `s == 0`.
- `< 60` seconds → `"{s}s"` (e.g. `45s`).

`LockoutMessage` becomes unused and is removed. The **unknown-email** cache-throttle path
(`LoginFailures_{email}`, line 140-144) is unchanged and keeps returning plain
`Invalid credentials.` — it must never reveal lockout state or time (account-enumeration
protection, per the existing auth design).

No other code paths change: `refreshToken`, `logout`, `verifyEmail`, and successful `login`
responses are untouched.

### 4. GraphQL schema / return types (what the frontend sees)

The `login` mutation's return type (`ApiResponseOfAuthResultDto`) gains two optional fields.
On a lockout failure the response is:

```json
{
  "data": {
    "login": {
      "succeeded": false,
      "message": "Account is temporarily locked due to too many failed login attempts. Please try again in 9m 32s.",
      "data": null,
      "errors": ["Account is temporarily locked due to too many failed login attempts. Please try again in 9m 32s."],
      "lockoutRemainingSeconds": 572,
      "lockoutEndsAtUtc": "2026-08-05T15:18:19Z"
    }
  }
}
```

Schema types:
- `lockoutRemainingSeconds: Int`
- `lockoutEndsAtUtc: String` (ISO-8601 UTC)

### 5. Frontend documentation — `AUTH_FRONTEND.md`

- Add a **`## Lockout response`** section: the `login` mutation schema snippet including the two
  new fields, the return-type field list, a sample lockout JSON (above), and countdown guidance
  (start the counter from `lockoutRemainingSeconds`, or derive it from
  `lockoutEndsAtUtc` to stay drift-free; on countdown expiry the user can retry `login`).
- Update the **Lockout UX** section: show the countdown from the returned fields.
- Update the error-message table's "Account locked" row to note the message now includes the
  remaining time.
- Add a note that unknown emails still receive only `Invalid credentials.` (no lockout fields).

## Non-goals

- No change to `AuthMutation.cs`, `AuthResultDto`, the refresh-token flow, or the lockout
  threshold/duration.
- No EF migration (no schema change).
- No lockout countdown on `verifyEmail`, `refreshToken`, or the unknown-email throttle.

## Verification

- Release build: `0 Error(s)`.
- Live smoke test (same approach as the auth work): lock `rluser1@yopmail.com` with 5 failed
  logins, then a 6th attempt returns `succeeded:false` with `lockoutRemainingSeconds ≈ 600`,
  `lockoutEndsAtUtc ≈ now+10min`, and the message contains the remaining time; clear the ban and
  confirm a correct login succeeds with the two fields `null`.
- `AUTH_FRONTEND.md` updated and consistent with the real schema.
