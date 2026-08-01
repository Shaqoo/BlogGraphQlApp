# Video Call Implementation — How It Works

> A complete, from-the-ground-up explanation of the 1-to-1 and group video/voice calling
> system in `BlogGraphQlApp`. The backend owns everything: Daily.co room lifecycle,
> meeting tokens, call state machines, realtime subscriptions, web push, webhook
> reconciliation, background cleanup and permanent call history.

---

## Table of contents

1. [Architecture at a glance](#1-architecture-at-a-glance)
2. [Daily.co integration](#2-dailyco-integration)
3. [Database model](#3-database-model)
4. [1-to-1 call flow (state machine)](#4-1-to-1-call-flow-state-machine)
5. [Group call flow](#5-group-call-flow)
6. [Realtime subscriptions (WebSocket topics)](#6-realtime-subscriptions-websocket-topics)
7. [Web Push notifications](#7-web-push-notifications)
8. [Daily webhooks — reconciliation](#8-daily-webhooks--reconciliation)
9. [Background cleanup service](#9-background-cleanup-service)
10. [Call history (permanent records)](#10-call-history-permanent-records)
11. [The two bugs we fixed to make this work](#11-the-two-bugs-we-fixed-to-make-this-work)
12. [Frontend contract (what the client does)](#12-frontend-contract-what-the-client-does)

---

## 1. Architecture at a glance

```
┌──────────────┐  GraphQL (/gql, WS /gql)   ┌───────────────────────────┐
│  Frontend    │ ─────────────────────────▶ │   ASP.NET Core + HotChocolate   │
│ (React +     │ ◀───────────────────────── │                              │
│  Daily SDK)  │                            │  VideoCallService            │
│              │                            │  GroupCallService            │
│              │  roomUrl + meeting token   │  DailyCallService (REST)     │
│              │ ◀───────────────────────── │  WebPushService             │
│              │                            │  DailyWebhookService         │
│              │                            │  DailyRoomCleanupService     │
│              │                            │  CallHistoryService          │
└──────────────┘                            └──────────────┬──────────────┘
                                                           │ REST (ApiKey)
                                                   ┌───────▼───────┐
                                                   │   Daily.co    │
                                                   │  rooms/tokens │
                                                   └───────────────┘
```

**Key design decisions**

- **Backend owns everything Daily.** Room creation, meeting-token generation and room
  deletion happen only server-side. The Daily API key never reaches the client. The
  frontend only ever receives a `roomUrl` + a `token`.
- **One call = one room = one random GUID.** Rooms are never reused and never permanent.
  A new, cryptographically random room name (`reelio_{callId}` for 1-to-1,
  `reelio_group_{callId}` for group calls) is generated per call.
- **Rooms are private** with a **30-minute expiry**, and `chat`, `screenshare` and
  `recording` are disabled in the room properties.
- **Two stores for every call:** a temporary `ActiveVideoCall` / `GroupVideoCall` row
  (mirrors the live room, deleted/superseded when the call ends) and a **permanent**
  `CallHistory` row (outlives the room; only the user can delete it).

---

## 2. Daily.co integration

### `DailyCallService` (`Services/Daily/DailyCallService.cs`)

A typed `HttpClient` wrapper around the Daily REST API
(`https://api.daily.co/v1`). Every request sends `Authorization: Bearer <ApiKey>`.

| Method | Daily endpoint | Purpose |
|---|---|---|
| `CreateRoomAsync(roomName, expiresAt, maxParticipants)` | `POST /rooms` | Create a private room. Sets `exp`, `max_participants`, disables chat/screenshare. Returns `DailyRoom(name, url)`. |
| `CreateMeetingTokenAsync(roomName, userName, isOwner, expiresAt)` | `POST /meeting-tokens` | Issue a meeting token for a participant. `isOwner: true` for the caller who created the call (owner can manage the room); `false` for the recipient/joiners. |
| `EndRoomAsync(roomName)` | `DELETE /rooms/{name}` | Delete the room immediately on call end. 404 is swallowed (already gone). |
| `GetRoomAsync(roomName)` | `GET /rooms/{name}` | Read the current participant count (used by webhooks/cleanup to detect empty rooms). |

Room payload sent to Daily:

```jsonc
{
  "name": "reelio_3f2c…",          // GUID-based, unique per call
  "privacy": "private",
  "properties": {
    "exp": 1750000000,              // unix seconds, +30 min
    "max_participants": 2,          // 1-to-1; group size for group calls
    "enable_chat": false,
    "enable_screenshare": false
  }
}
```

Meeting-token payload:

```jsonc
{
  "properties": {
    "room_name": "reelio_3f2c…",
    "user_name": "Shaqo",
    "is_owner": true,
    "exp": 1750000000,
    "enable_screenshare": false
  }
}
```

> **Fallback URL:** the API response normally includes `url`
> (`https://Shaqoo.daily.co/reelio_3f2c…`). If it is ever missing, the service builds
> `https://{Daily__Subdomain}.daily.co/{roomName}` via `BuildMeetingUrl`. The frontend
> should always use the `roomUrl` returned by the backend.

---

## 3. Database model

### Temporary / live-call state

**`ActiveVideoCall`** (1-to-1, one row per live call)
- `CallId` (stable GUID, used in URLs and subscription topics)
- `RoomName`, `DailyRoomUrl`
- `CallerId`, `RecipientId` (+ navigation to `User`)
- `Status` (`Ringing | Accepted | Connected | Rejected | Ended | Missed`)
- `ConnectedAt`, `EndedAt`

**`GroupVideoCall`** (group calls)
- `CallId`, `GroupId`, `RoomName`, `DailyRoomUrl`, `StartedBy`
- `Status` (`Ringing | Connected | Ended`)
- `MediaType` (`Voice | Video`), `EndedAt`

**`GroupVideoCallParticipant`** (per-participant row for a live group call)
- `CallId`, `UserId` (unique pair), `Token` (the Daily token, **nulled on call end**),
  `JoinedAt`, `LeftAt`, plus current media state (`IsMuted`, `CameraEnabled`,
  `ScreenSharing`, `HandRaised`).

### Permanent records

**`CallHistory`** (one row per call ever started)
- `CallId` (unique index), `CallType` (`Direct | Group`)
- `CallerId`, `RecipientId?`, `GroupId?`
- `RoomName` (for auditing), `StartedAt`, `AnsweredAt?`, `EndedAt?`, `DurationSeconds`
- `Status` (`Ringing | Connected | Completed | Missed | Rejected | Cancelled`)
- `EndedByUserId?`

**`GroupCallParticipantHistory`** (snapshot of each group-call participant)
- `CallHistoryId`, `UserId`, `JoinedAt?`, `LeftAt?`, `DurationSeconds`

---

## 4. 1-to-1 call flow (state machine)

Handled by `VideoCallService` (`Services/Video/VideoCallService.cs`). All methods return
`ApiResponse<T>` and publish realtime events via `ITopicEventSender`.

```
        startVideoCall(recipientId)
                   │
                   ▼
             RINGING ──────────────► (60s timeout → MISSED, room deleted)
             │     │
   rejectVideoCall │  acceptVideoCall
             │     │
             ▼     ▼
        REJECTED   ACCEPTED ──► webhook participant.joined ──► CONNECTED
                    │                                            │
                    │        endVideoCall (either side)          │
                    │            OR webhook room empty           │
                    └────────────► ENDED (room deleted) ◄────────┘
```

### Start (`startVideoCall(recipientId)`)

1. **Guards:** not calling yourself; recipient exists; **neither** party already in a
   call (`HasActiveCallAsync` checks Ringing/Accepted/Connected).
2. Generate `callId = Guid.NewGuid()`, `roomName = $"reelio_{callId:N}"`,
   `expiresAt = now + 30 min`.
3. `CreateRoomAsync(roomName, expiresAt, maxParticipants: 2)`.
4. `CreateMeetingTokenAsync(..., isOwner: true)` for the **caller** (owner token, so the
   caller can join immediately).
5. Persist `ActiveVideoCall { Status = Ringing }`, commit.
6. `CallHistoryService.StartDirectAsync(...)` writes the permanent history row (Ringing).
7. `WebPushService.SendToUserAsync(recipient, IncomingCallPushPayload)`.
8. Publish `VideoCallDto` to topic `"{recipientId}_IncomingCall"`.
9. Return the caller's token (status `Ringing`).

### Accept (`acceptVideoCall(callId)`)

- Only the **recipient**; call must be `Ringing`.
- Issue a **non-owner** token for the recipient (expires +30 min).
- Set `Status = Accepted`, commit.
- `CallHistoryService.MarkAnsweredAsync(...)` → history becomes `Connected`.
- Publish `"{callerId}_CallAccepted"` with the recipient's token.
- The caller opens the room URL, and the webhook (`participant.joined`) moves the call
  to `Connected`.

### Reject (`rejectVideoCall(callId)`)

- Only the recipient; must not already be finished.
- `EndRoomAsync` (delete the Daily room), set `Status = Rejected`, `EndedAt = now`,
  commit.
- `CallHistoryService.RejectDirectAsync(...)` → history `Rejected`.
- Publish `"{callerId}_CallRejected"`.

### End (`endVideoCall(callId)`)

- Either participant; must not already be finished.
- `EndRoomAsync`, set `Status = Ended`, `EndedAt = now`, commit.
- `CallHistoryService.EndDirectAsync(...)` → history `Completed` (if answered, duration
  = ended − answered) else `Cancelled`.
- Publish `"{otherParticipantId}_CallEnded"`.

### Token refresh (`videoCallToken(callId)`)

- Either participant; re-issues a fresh Daily token (+30 min) — used after a page
  refresh or token expiry. Caller gets an owner token, recipient a non-owner token.

---

## 5. Group call flow

Handled by `GroupCallService` (`Services/Groups/GroupCallService.cs`). The states are
simpler: `Ringing → Connected → Ended`.

```
   startGroupCall(groupId, mediaType)
        │
        ▼
     RINGING  ──► first joinGroupCall ──► CONNECTED
        │                                   │
        │  (5 min timeout → ENDED)           │  endGroupCall / webhook empty / 30 min
        └───────────────────────────────────► ENDED (room deleted, tokens nulled)
```

### Start (`startGroupCall(groupId, mediaType)`) — any member

1. Guard: user must be a group member; not already in a group call.
2. `roomName = $"reelio_group_{callId:N}"`, `maxParticipants = group.MemberCount`.
3. Create room + **owner** token for the starter.
4. Persist `GroupVideoCall { Status = Ringing, MediaType }`.
5. `CallHistoryService.StartGroupAsync(...)` (history + the starter's participant row).
6. Push `GroupCallPushPayload` to **every other member** + publish
   `"{groupId}_GroupCallStarted"`.

### Join (`joinGroupCall(callId)`) — any member

1. Guard: member of the group; call not ended.
2. Issue a **non-owner** token for the joining user.
3. Upsert the participant row (joined again → clear `LeftAt`).
4. First join moves the call to `Connected` (and history `MarkAnsweredAsync`).
5. Publish `"{callId}_GroupCallParticipantJoined"` (and to the group when first join).

### State toggles (`toggleGroupCallMute/Camera/Screenshare/HandRaised`)

- Flip the flag on the participant row, commit, publish
  `"{callId}_GroupCallParticipantUpdated"` so every tile updates live.

### Leave / End (`leaveGroupCall`, `endGroupCall`)

- **Leave:** set `LeftAt`, null the token, publish
  `"{callId}_GroupCallParticipantLeft"`.
- **End (any member):** `EndRoomAsync`, `Status = Ended`, null all participant tokens,
  publish `"{groupId}_GroupCallEnded"` + `"{callId}_GroupCallEnded"`,
  `CallHistoryService.EndGroupAsync` → history `Completed` if anyone besides the caller
  joined, else `Cancelled`.

### Group call history (`groupCallHistory(groupId)`, `/api/call-history`)

Per-participant durations are computed at end time from `JoinedAt`/`LeftAt`.

---

## 6. Realtime subscriptions (WebSocket topics)

Defined in `GraphQL/Subscriptions/CallSubscription.cs` and
`GraphQL/Subscriptions/NotificationSubscription.cs`. Clients subscribe by id and receive
typed payloads. Topic naming convention: `{userId}_Event` or `{groupId}_Event`.

### 1-to-1 call topics

| Subscription | Topic | Payload | When |
|---|---|---|---|
| `incomingCall(userId)` | `{userId}_IncomingCall` | `VideoCallDto` | a call rings `userId` |
| `callAccepted(userId)` | `{userId}_CallAccepted` | `VideoCallDto` | a call started by `userId` is accepted (contains recipient token) |
| `callRejected(userId)` | `{userId}_CallRejected` | `VideoCallDto` | a call started by `userId` is rejected |
| `callEnded(userId)` | `{userId}_CallEnded` | `VideoCallDto` | a call the user is in ends |
| `callMissed(userId)` | `{userId}_CallMissed` | `VideoCallDto` | a call started by `userId` was missed |

### Group call topics

| Subscription | Topic | Payload |
|---|---|---|
| `groupCallStarted(groupId)` | `{groupId}_GroupCallStarted` | `GroupCallDto` |
| `groupCallEnded(groupId)` | `{groupId}_GroupCallEnded` | `GroupCallDto` |
| `groupCallParticipantJoined(callId)` | `{callId}_GroupCallParticipantJoined` | `GroupCallParticipantDto` |
| `groupCallParticipantLeft(callId)` | `{callId}_GroupCallParticipantLeft` | `GroupCallParticipantDto` |
| `groupCallParticipantUpdated(callId)` | `{callId}_GroupCallParticipantUpdated` | `GroupCallParticipantDto` |

### Notifications topics

| Subscription | Topic | Payload |
|---|---|---|
| `onNotificationReceived(userId)` | `{userId}_User_NotificationReceived` | `NotificationDto` |
| `onNotificationRead(userId)` | `{userId}_User_NotificationRead` | `NotificationDto` |

> All `PublishAsync` helpers are generic (`PublishAsync<T>(topic, payload)`). See
> [§11](#11-the-two-bugs-we-fixed-to-make-this-work) for why.

---

## 7. Web Push notifications

`WebPushService` (`Services/Push/WebPushService.cs`) — RFC 8030 Web Push with VAPID.

- **Registration:** the client calls `registerPushSubscription(endpoint, p256dh, auth)`
  once per browser after permission; subscriptions are stored per user
  (`UserWebPushSubscription`, unique endpoint).
- **Delivery:** `SendToUserAsync(userId, payload)` / `SendToUsersAsync(userIds, payload)`
  load the user's subscriptions and send concurrently (max 5 in flight). Dead
  subscriptions (HTTP 404/410) are removed automatically.
- **VAPID keys** live in `.env` (`WebPush__*`, gitignored); the **public** key is what
  the frontend passes as `applicationServerKey` to `PushManager.subscribe`.

Push payloads the client must handle:

```jsonc
// 1-to-1 incoming call
{ "type": "video_call", "callId": "…", "roomName": "…", "callerId": "…",
  "callerName": "…", "callerAvatar": "…", "url": "/call/{callId}" }

// group call
{ "type": "group_call", "callId": "…", "groupId": "…", "groupName": "…",
  "roomName": "…", "startedById": "…", "startedByName": "…", "url": "/call/{callId}" }

// missed call
{ "type": "call_missed", "callId": "…", "roomName": "…", "callerId": "…", "url": "/call/{callId}" }
```

---

## 8. Daily webhooks — reconciliation

`DailyWebhookService` (`Services/Daily/DailyWebhookService.cs`) handles
`POST /api/daily/webhook` (anonymous). It accepts `participant.joined`,
`participant.left`, `room.finished`, `call.connected`, `call-ended`. It is **read-only
safe** — it only touches rows that match a known `roomName`, and it asks Daily
`GET /rooms/{room}` before deleting anything (`IsRoomEmptyAsync`).

- **`participant.joined` / `call.connected`** on an `Accepted` 1-to-1 call →
  `Status = Connected`, `ConnectedAt`, history `MarkAnswered`.
- **`participant.left` / `room.finished` / `call-ended`** + room now empty →
  delete the room, `Status = Ended`, publish `CallEnded` to **both** sides, history
  `EndDirectAsync`.
- The same logic exists for group calls (join → `Connected`; empty → `Ended`, tokens
  nulled, `GroupCallEnded` published).

The webhook gives the system a second source of truth: even if a client never calls
`endVideoCall`, an empty room ends the call correctly.

---

## 9. Background cleanup service

`DailyRoomCleanupService` (`BackgroundServices/DailyRoomCleanupService.cs`) — a
`BackgroundService` that runs every 60 seconds and:

1. **Missed calls:** 1-to-1 calls still `Ringing` after **60 s** → delete room,
   `Status = Missed`, push `call_missed`, publish `{callerId}_CallMissed`, history
   `MissDirectAsync`.
2. **Stale rooms (safety net):** finished calls (`Ended`/`Rejected`/`Missed`) older than
   **30 min** → delete any lingering Daily room, history `EndDirectAsync`.
3. **Group calls:** still `Ringing` after **5 min** → end + delete room; `Ended` older
   than **30 min** → delete room. All participant tokens are nulled.

This guarantees **no temporary Daily room ever outlives its call** (see `AGENTS.md`).

---

## 10. Call history (permanent records)

`CallHistoryService` (`Services/CallHistory/CallHistoryService.cs`) writes exactly one
record per call at start, then a **single guarded terminal transition**
(`IsFinal` guard prevents double transitions from racing webhooks + cleanup + user
actions).

| Lifecycle method | Terminal mapping |
|---|---|
| `StartDirectAsync` / `StartGroupAsync` | `Ringing` (starter gets a participant row for group) |
| `MarkAnsweredAsync` | `Ringing → Connected` |
| `EndDirectAsync` | `Completed` (if answered, duration = ended − answered) else `Cancelled` |
| `RejectDirectAsync` | `Rejected` |
| `MissDirectAsync` | `Missed` |
| `EndGroupAsync` | `Completed` if anyone besides the caller joined, else `Cancelled` |

REST surface (`/api/call-history`, all `RequireAuthorization`): paginated list
(`GET /api/call-history`), single (`GET .../{id}`), delete one (`DELETE .../{id}`),
clear all (`DELETE ...`). **Access** is limited to caller, recipient, or member of the
group. `isIncoming` is derived per viewer (Group → `callerId != viewerId`; Direct →
`recipientId == viewerId`).

---

## 11. The two bugs we fixed to make this work

### Bug 1 — `InvalidMessageTypeException` on every publish

**Symptom:** every call/message publish threw
`HotChocolate.Subscriptions.InvalidMessageTypeException: The topic already exists with a
different message type. Topic message type: VideoCallDto. Requested message type:
System.Object`, because the six `PublishAsync` helpers were declared as
`PublishAsync(string topic, object payload, ...)`.

**Fix:** made them generic —
`PublishAsync<T>(string topic, T payload, ...)` (now calling
`events.SendAsync(topic, payload)`, which infers the correct message type). Applied in
`VideoCallService`, `GroupCallService`, `GroupMessageService`, `GroupService`,
`DailyWebhookService`, `DailyRoomCleanupService`. Subscriptions now deliver correctly
typed payloads end-to-end.

### Bug 2 — subscriptions rejected with `AUTH_NOT_AUTHENTICATED`

**Symptom:** even with a valid JWT, WebSocket subscriptions were rejected because **no
code authenticated the `connection_init` payload**, and browsers cannot set HTTP headers
on the WebSocket handshake.

**Fix:** added `GraphQL/Subscriptions/SocketSessionInterceptor.cs` (extends
HotChocolate's `DefaultSocketSessionInterceptor`). On `OnConnectAsync` it reads the JWT
from the `connection_init` payload (keys `Authorization` / `authorization` / `authToken`
/ `access_token`, `"Bearer "` stripped), validates it with the same
`Jwt:Key`/`Issuer`/`Audience` parameters used by the REST pipeline, and on success sets
`session.Connection.HttpContext.User`. The `Authorization` header from the upgrade
request is used as a fallback. Registered in `Program.cs` via
`.AddSocketSessionInterceptor<SocketSessionInterceptor>()`.

### Bonus — transactions vs. the retrying execution strategy

MySQL's `MySqlRetryingExecutionStrategy` rejects user-initiated transactions
("does not support user-initiated transactions"). Group flows that need atomic writes
now go through `IUnitOfWork.ExecuteInTransactionAsync(Func<Task>)`, which uses
`CreateExecutionStrategy().ExecuteAsync(...)` so the whole operation block can be
retried safely.

---

## 12. Frontend contract (what the client does)

The frontend **never** calls the Daily API. It only:

1. **Subscribes** on app load: `incomingCall(myUserId)`, `callAccepted(myUserId)`,
   `callRejected`, `callEnded`, `callMissed`, `onNotificationReceived(myUserId)`, and —
   per open group — `groupCallStarted`, `groupCallEnded`,
   `groupCallParticipantJoined/Left/Updated(callId)`.
2. **Starts calls** with `startVideoCall(recipientId)` / `startGroupCall(groupId, mediaType)`.
3. **Answers** with `acceptVideoCall(callId)` (recipient) or `joinGroupCall(callId)`
   (group) — these return the Daily `roomUrl` **and** the joining `token`.
4. **Joins media:** opens `roomUrl` in Daily Prebuilt (iframe / `DailyProvider`) and
   passes `token`. Reacts to `callAccepted`/`incomingCall` with the token to enter the
   room. Re-issues a token via `videoCallToken(callId)` / `groupCallToken(callId)` after
   refresh/expiry.
5. **Ends calls** with `endVideoCall(callId)` / `endGroupCall(callId)` (or the room
   being left empty / timing out ends it server-side).
6. **Shows call history** from `GET /api/call-history`.

The incoming-call overlay is driven by the `incomingCall` subscription while the app is
foregrounded and by Web Push when it is backgrounded.
