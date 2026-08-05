# Authentication — Refresh Tokens & Login Lockout (Frontend Guide)

All GraphQL operations are at `POST /gql`. Auth is now a **two-token** scheme: a short-lived
access token plus a long-lived, one-time-use refresh token.

## What changed

- `login` and `verifyEmail` no longer return a raw JWT string in `data`. They now return an
  `AuthResult` object.
- Access tokens last **30 minutes** (down from 24 hours) and must be refreshed.
- Refresh tokens last **30 days** and are **rotated**: every refresh consumes the old one and
  issues a fresh pair. A refresh token is single-use.
- After **5 failed login attempts**, an account is locked for **10 minutes**.
- A `logout` mutation revokes a device's refresh token server-side.

## Login / verifyEmail — new response shape

Mutation:

```graphql
mutation Login($email: String!, $password: String!) {
  login(input: { email: $email, password: $password }) {
    succeeded
    message
    data {
      accessToken
      refreshToken
      expiresIn
    }
    errors
  }
}
```

`expiresIn` is the access-token lifetime in seconds (1800). Store both tokens; `refreshToken`
must be sent ONLY to the `refreshToken` and `logout` mutations — never as an `Authorization`
header.

## Refreshing an expired access token

Mutation (no auth header needed):

```graphql
mutation Refresh($refreshToken: String!) {
  refreshToken(input: { refreshToken: $refreshToken }) {
    succeeded
    message
    data {
      accessToken
      refreshToken
      expiresIn
    }
    errors
  }
}
```

Client flow:

1. Attach `Authorization: Bearer <accessToken>` to every authenticated request and WS
   `connection_init` (GraphQL-over-WebSocket uses the `Authorization` header; the SignalR
   presence hub uses the `?access_token=` query param).
2. On an `Unauthorized`/token error, call `refreshToken` once with the stored refresh token.
3. On success, replace both stored tokens and retry the original request. Keep a single
   in-flight refresh promise so concurrent 401s share it.
4. On failure, the refresh token is invalid/expired/revoked → clear local tokens and route to
   the login screen.

## Logout (revokes the device session)

```graphql
mutation Logout($refreshToken: String!) {
  logout(input: { refreshToken: $refreshToken }) {
    succeeded
    message
  }
}
```

Requires the `Authorization` header. Afterwards clear both tokens locally.

## Error messages you may show

| Operation | `message` |
| --- | --- |
| Bad email/password | `Invalid credentials.` |
| Account locked | `Account is temporarily locked due to too many failed login attempts. Please try again in 9m 32s.` (includes the remaining time) |
| Refresh token invalid | `Invalid refresh token.` |
| Refresh token reused/revoked | `Refresh token has been revoked.` |
| Refresh token expired | `Refresh token has expired.` |
| Unverified email on login | `Login successful, but your email is not verified. A new verification code has been sent to your email.` |

## Lockout UX

After 5 failed logins the account is locked for 10 minutes (even with the correct password). Show
the lockout message and render a countdown from the response's `lockoutRemainingSeconds` /
`lockoutEndsAtUtc` fields (see below); disable the submit button until it expires. Unknown emails
always get `Invalid credentials.` — never reveal whether an account exists.

## Lockout response (countdown data)

When `login` fails because the account is locked, `succeeded` is `false`, `data` is `null`, and the
response envelope carries two **nullable** fields with the lockout window:

- `lockoutRemainingSeconds: Int` — whole seconds until the account unlocks (rounded up).
- `lockoutEndsAtUtc: String` — ISO-8601 UTC timestamp of when the lockout expires.

Both fields are `null` on every non-lockout response. Sample:

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

Client flow:

1. On a failed `login` with `succeeded: false`, check for `lockoutRemainingSeconds`.
2. Start the countdown from `lockoutRemainingSeconds`, or better, compute the remaining time from
   `lockoutEndsAtUtc` (stays accurate across clock drift and slow round-trips).
3. Disable the submit button while counting down; when the counter reaches zero the user can retry
   `login` (a retry will either succeed or return a fresh lockout response).
4. Never show the countdown for `Invalid credentials.` (bad credentials or unknown email) — that
   message carries no lockout fields.

## Security notes

- Never log tokens or put the refresh token in a URL.
- The refresh token is single-use; if a client replays an old one, the backend revokes all of
  that user's sessions as a compromise response.
