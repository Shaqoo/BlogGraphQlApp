# Realtime Video Calls, Group Chat & Web Push — Backend Reference

> Complete documentation of the realtime features recently added to this project:
> **1-to-1 video calls (Daily.co)**, **group chat**, **group video calls**, and
> **Web Push notifications**, exposed entirely through GraphQL.
>
> This file also contains a **standard prompt for a UI/UX developer** (Section 10)
> that can be handed off as-is to build the frontend against this backend.

---

## Table of contents

1. [Overview](#1-overview)
2. [New files & architecture](#2-new-files--architecture)
3. [Configuration & secrets](#3-configuration--secrets)
4. [Enums (GraphQL types)](#4-enums-graphql-types)
5. [DTOs (data shapes)](#5-dtos-data-shapes)
6. [GraphQL API — Queries](#6-graphql-api--queries)
7. [GraphQL API — Mutations](#7-graphql-api--mutations)
8. [GraphQL API — Subscriptions](#8-graphql-api--subscriptions)
9. [Call lifecycle, business rules & background jobs](#9-call-lifecycle-business-rules--background-jobs)
10. [Standard prompt for a UI/UX developer](#10-standard-prompt-for-a-uiux-developer)

---

## 1. Overview

Three interconnected features were added to the existing ASP.NET Core + HotChocolate
GraphQL backend:

| Feature | What it does |
|---|---|
| **1-to-1 video calls** | Call another user; Daily.co rooms + tokens are created/owned by the backend. States: `RINGING → ACCEPTED → CONNECTED → ENDED`, plus `REJECTED` and `MISSED`. |
| **Web Push notifications** | Browser subscriptions stored per user (VAPID auth). Callers/recipients/group members are notified when calls start, are accepted, rejected, missed or ended. |
| **Group chat** | Create groups, add/remove members (friends only), roles (`OWNER`/`ADMIN`/`MEMBER`), send & read messages. |
| **Group video calls** | Start/join/end a call for a whole group; every member gets a push + realtime notification. |
| **Call history** | Permanent record of every direct/group call, kept after the temporary Daily room is deleted. Read/delete via a small authenticated REST API (`/api/call-history`). |

Design decisions:

- **Everything is backend-owned.** Room creation, meeting-token generation and room
  deletion only happen server-side; the Daily API key never reaches the client.
- **Rooms are private**, expire after **30 minutes**, meeting tokens have a **30-minute
  TTL**, and `chat`, `screenshare`, `recording` and `transcription` are disabled.
- **Call history outlives the room.** Deleting a Daily room never deletes the history
  row; only the user can remove it (REST DELETE). Terminal transitions (Completed /
  Missed / Rejected / Cancelled) are written once via `ICallHistoryService`.
- **State transitions happen both from GraphQL actions and from Daily webhooks** (a
  participant joining marks a call `CONNECTED`; an empty room ends the call).
- **A background service** sweeps the database every minute to clean up unanswered
  calls (→ `MISSED`) and stale rooms.

---

## 2. New files & architecture

```
Enums/
  VideoCallStatus.cs            RINGING, ACCEPTED, CONNECTED, REJECTED, ENDED, MISSED
  GroupCallStatus.cs            RINGING, CONNECTED, ENDED
  GroupMemberRole.cs            OWNER, ADMIN, MEMBER
  CallType.cs                   DIRECT, GROUP
  CallHistoryStatus.cs          RINGING, CONNECTED, COMPLETED, MISSED, REJECTED, CANCELLED

Entities/                       9 new database tables (via EF migration)
  ActiveVideoCall.cs            CallId, RoomName, DailyRoomUrl, Caller, Recipient, Status, ConnectedAt, EndedAt
  ChatGroup.cs                  Name, ImageUrl, CreatedBy
  ChatGroupMember.cs            GroupId, UserId, Role, JoinedAt   (unique GroupId+UserId)
  GroupMessage.cs               GroupId, SenderId, Text, EditedAt, Deleted
  GroupVideoCall.cs             CallId, GroupId, RoomName, DailyRoomUrl, StartedBy, Status, EndedAt
  GroupVideoCallParticipant.cs  CallId, UserId, Token, JoinedAt, LeftAt  (unique CallId+UserId)
  UserWebPushSubscription.cs    UserId, Endpoint (unique), P256dh, Auth
  CallHistory.cs                permanent record: CallId (unique), CallType, Caller, Recipient?,
                                Group?, RoomName, StartedAt, AnsweredAt?, EndedAt?, DurationSeconds,
                                Status, EndedByUser?
  GroupCallParticipantHistory.cs CallHistoryId, User, JoinedAt?, LeftAt?, DurationSeconds

Configurations/EfConfigs/      One IEntityTypeConfiguration per new entity (indexes, lengths, FKs)

DTOs/
  VideoCallDto, GroupDto, GroupMemberDto, GroupMessageDto, GroupCallDto
  IncomingCallPushPayload, GroupCallPushPayload
  CallHistoryDto, CallHistoryParticipantDto, CallHistoryQuery

Services/
  Daily/IDailyCallService, DailyCallService, DailyApiException, DailyRoom, DailyRoomStatus
  Daily/DailyWebhookService              Parses Daily webhooks and updates call state
  Push/IWebPushService, WebPushService   VAPID registration + batched delivery (max 5 concurrent)
  Video/IVideoCallService, VideoCallService
  Groups/IGroupService, GroupService, GroupPermissions, IGroupCallService, GroupCallService
  History/ICallHistoryService, CallHistoryService
                                          Permanent history lifecycle + query/delete with access rules

GraphQL/
  Queries/   VideoCallQueries, GroupQueries
  Mutations/ VideoCallMutations, WebPushMutations, GroupMutations, GroupCallMutations
  Subscriptions/CallSubscription
  Types/     VideoCallTypeGql, GroupTypeGql, GroupMemberTypeGql, GroupMessageTypeGql, GroupCallTypeGql
  ClaimsPrincipalExtensions.cs           Reads the JWT NameIdentifier claim -> user id

BackgroundServices/
  DailyRoomCleanupService                Every 60s: missed calls, stale rooms, group call cleanup

Endpoints/
  DailyWebhookEndpoint.cs                POST /api/daily/webhook (anonymous)
  CallHistoryEndpoints.cs                GET/DELETE /api/call-history (RequireAuthorization)

Settings/
  DailySettings   (section "Daily")      ApiKey, BaseUrl (default https://api.daily.co/v1)
  VapidSettings   (section "WebPush")    Subject, PublicKey, PrivateKey

Migrations/
  20260801112859_VideoCallsGroupsAndPush  Creates the 7 call/group/push tables + indexes
  20260801133050_CallHistory              Creates CallHistories + GroupCallParticipantHistories
```

---

## 3. Configuration & secrets

### appsettings.json (placeholders only)

```jsonc
"Daily": { "ApiKey": "", "BaseUrl": "https://api.daily.co/v1", "Subdomain": "" },
"WebPush": { "Subject": "mailto:you@example.com", "PublicKey": "", "PrivateKey": "" }
```

### .env (gitignored — real values)

```bash
Daily__ApiKey=your_daily_api_key            # from https://dashboard.daily.co/developers
Daily__BaseUrl=https://api.daily.co/v1
Daily__Subdomain=Shaqoo                     # public subdomain, used to build the meeting URL

WebPush__Subject=mailto:shakirullahohio@gmail.com
WebPush__PublicKey=your_vapid_public_key
WebPush__PrivateKey=your_vapid_private_key
```

### Generate a VAPID key pair

```bash
npx web-push generate-vapid-keys
```

> **Security notes**
> - The Daily API key and the VAPID **private** key are secrets. Never commit them.
> - If a key was ever shared in chat/commit, rotate it (Daily dashboard / new VAPID pair)
>   and re-run push registration on clients (the VAPID public key is embedded in the
>   browser subscription via `applicationServerKey`).
> - The VAPID **public** key must be made available to the frontend (it is the
>   `applicationServerKey` passed to `PushManager.subscribe`).
> - `Daily__ApiKey` does **not** include the subdomain. The subdomain (`Shaqoo.daily.co`)
>   is already embedded in the `roomUrl` returned by the backend (and used as a fallback
>   via `Daily__Subdomain`), so the frontend never needs to construct Daily URLs itself.
> - See `AGENTS.md` → **Daily Room Management** for the rules agents must follow when
>   working on this feature (unique room per call, backend-only room/token creation,
>   immediate deletion on call end, background cleanup).

---

## 4. Enums (GraphQL types)

These appear in payloads as GraphQL enums (upper-case SCREAMING_SNAKE_CASE):

```graphql
enum VideoCallStatus { RINGING ACCEPTED CONNECTED REJECTED ENDED MISSED }
enum GroupCallStatus { RINGING CONNECTED ENDED }
enum GroupMemberRole { OWNER ADMIN MEMBER }
enum CallType { DIRECT GROUP }
enum CallHistoryStatus { RINGING CONNECTED COMPLETED MISSED REJECTED CANCELLED }
```

`CallHistoryStatus` uses transient states (RINGING → CONNECTED) while a call is live and
a single **terminal** state (COMPLETED / MISSED / REJECTED / CANCELLED) once it ends:
`EndDirectAsync` → COMPLETED if answered else CANCELLED; `RejectDirectAsync` → REJECTED;
`MissDirectAsync` → MISSED; `EndGroupAsync` → COMPLETED if anyone besides the caller
joined else CANCELLED.

---

## 5. DTOs (data shapes)

Every mutation/query returns an `ApiResponse<T>` wrapper:

```graphql
type ApiResponseOfX {
  succeeded: Boolean!
  data: X            # null when the operation failed
  message: String
  errors: [String!]
}
```

### VideoCallDto — 1-to-1 calls

| Field | Type | Notes |
|---|---|---|
| `callId` | UUID | Stable id, used in subscription topics and URLs |
| `roomName` | String | Daily room name |
| `roomUrl` | String | Full Daily room URL (use this, not subdomain) |
| `token` | String | Daily meeting token. Only set after `acceptVideoCall` / `videoCallToken` |
| `callerId` | UUID | The user who started the call |
| `callerName` | String | |
| `callerAvatar` | String | nullable |
| `recipientId` | UUID | The user being called |
| `status` | VideoCallStatus | |
| `createdAt` | DateTime | |
| `endedAt` | DateTime | nullable |

### GroupDto — group chat

| Field | Type |
|---|---|
| `id` | UUID |
| `name` | String |
| `imageUrl` | String (nullable) |
| `createdBy` | UUID |
| `createdByName` | String |
| `createdAt` | DateTime |
| `memberCount` | Int |

### GroupMemberDto

| Field | Type |
|---|---|
| `id` | UUID |
| `groupId` | UUID |
| `userId` | UUID |
| `username` | String |
| `fullName` | String |
| `avatar` | String (nullable) |
| `role` | String (`"Owner"`, `"Admin"`, `"Member"`) |
| `joinedAt` | DateTime |

### GroupMessageDto

| Field | Type |
|---|---|
| `id` | UUID |
| `groupId` | UUID |
| `senderId` | UUID |
| `senderName` | String |
| `senderAvatar` | String (nullable) |
| `text` | String |
| `createdAt` | DateTime |
| `editedAt` | DateTime (nullable) |
| `deleted` | Boolean |

### GroupCallDto

| Field | Type |
|---|---|
| `callId` | UUID |
| `groupId` | UUID |
| `groupName` | String |
| `roomName` | String |
| `roomUrl` | String |
| `token` | String (only after `joinGroupCall` / `groupCallToken`) |
| `startedBy` | UUID |
| `startedByName` | String |
| `status` | GroupCallStatus |
| `createdAt` | DateTime |
| `endedAt` | DateTime (nullable) |

### Web push payloads (sent to the browser as JSON body)

`IncomingCallPushPayload` (1-to-1):

```json
{ "type": "video_call", "callId": "...", "roomName": "...", "callerId": "...",
  "callerName": "...", "callerAvatar": "...", "url": "/call/{callId}" }
```

`GroupCallPushPayload` (group):

```json
{ "type": "group_call", "callId": "...", "groupId": "...", "groupName": "...",
  "roomName": "...", "startedById": "...", "startedByName": "...", "url": "/call/{callId}" }
```

When a call is missed a push is sent with `"type": "call_missed"`.

### CallHistoryDto (returned by `/api/call-history`)

| Field | Type | Notes |
|---|---|---|
| `id` | UUID | record id |
| `callId` | UUID | stable call id (unique index) |
| `callType` | String (`"Direct"` / `"Group"`) | |
| `callerId` | UUID | |
| `callerName` / `callerAvatar` | String / String? | |
| `recipientId` | UUID? | null for group calls |
| `recipientName` / `recipientAvatar` | String? / String? | |
| `groupId` | UUID? | set for group calls |
| `groupName` | String? | |
| `startedAt` | DateTime | |
| `answeredAt` | DateTime? | |
| `endedAt` | DateTime? | |
| `durationSeconds` | Int | 0 when unanswered |
| `status` | CallHistoryStatus | terminal once ended |
| `endedByUserId` | UUID? | |
| `isIncoming` | Boolean | derived per viewer: Group → `callerId != viewerId`, Direct → `recipientId == viewerId` |
| `participants` | [CallHistoryParticipantDto!] | group calls only: `userId`, `username`, `fullName`, `avatar`, `joinedAt`, `leftAt`, `durationSeconds` |

---

## 6. GraphQL API — Queries

Endpoint: `POST /gql` (also GraphQL-over-WebSocket for subscriptions).
**All queries/mutations require `Authorization: Bearer <jwt>`.**

```graphql
# 1-to-1 call state (you must be caller or recipient)
videoCall(callId: UUID!): ApiResponseOfVideoCallDto

# Group chat (you must be a member)
groups:                      ApiResponseOfIEnumerableOfGroupDto
group(groupId: UUID!):       ApiResponseOfGroupDto
groupMembers(groupId: UUID!): ApiResponseOfIEnumerableOfGroupMemberDto
groupMessages(groupId: UUID!): ApiResponseOfIEnumerableOfGroupMessageDto

# Group call state (you must be a group member)
groupCall(callId: UUID!):    ApiResponseOfGroupCallDto
```

---

## 7. GraphQL API — Mutations

### 1-to-1 video calls

```graphql
# Start a Daily.co call to recipientId. Notifies the recipient (push + realtime).
# Returns the caller's token so they can join immediately.
startVideoCall(recipientId: UUID!): ApiResponseOfVideoCallDto

# Accept a ringing call. Only the recipient can do this. Returns the recipient token.
acceptVideoCall(callId: UUID!): ApiResponseOfVideoCallDto

# Reject a ringing call (recipient only).
rejectVideoCall(callId: UUID!): ApiResponseOfBoolean

# End an ongoing call (either participant). Deletes the Daily room.
endVideoCall(callId: UUID!): ApiResponseOfBoolean

# Get a fresh token (e.g. after a page refresh / token expiry).
videoCallToken(callId: UUID!): ApiResponseOfVideoCallDto
```

### Web push subscription (call once per browser, after user consent)

```graphql
registerPushSubscription(endpoint: String!, p256dh: String!, auth: String!): ApiResponseOfBoolean
unregisterPushSubscription(endpoint: String!): ApiResponseOfBoolean
```

### Group chat

```graphql
createGroup(name: String!, imageUrl: String): ApiResponseOfGroupDto
updateGroup(groupId: UUID!, name: String!, imageUrl: String): ApiResponseOfGroupDto   # Owner/Admin
deleteGroup(groupId: UUID!): ApiResponseOfBoolean                                     # Owner only

addGroupMember(groupId: UUID!, userId: UUID!): ApiResponseOfBoolean   # Owner/Admin, friends only
removeGroupMember(groupId: UUID!, userId: UUID!): ApiResponseOfBoolean
leaveGroup(groupId: UUID!): ApiResponseOfBoolean                       # Owner cannot leave (delete instead)

promoteGroupAdmin(groupId: UUID!, userId: UUID!): ApiResponseOfBoolean # Owner only
demoteGroupAdmin(groupId: UUID!, userId: UUID!): ApiResponseOfBoolean  # Owner only

sendGroupMessage(groupId: UUID!, text: String!): ApiResponseOfGroupMessageDto
```

### Group video calls

```graphql
startGroupCall(groupId: UUID!): ApiResponseOfGroupCallDto   # any member; notifies all other members
joinGroupCall(callId: UUID!): ApiResponseOfGroupCallDto     # returns the join token
endGroupCall(callId: UUID!): ApiResponseOfBoolean           # any member
groupCallToken(callId: UUID!): ApiResponseOfGroupCallDto
```

### Errors

- Business rule failures are returned as `ApiResponse` with `succeeded: false`,
  `message` and `errors` (HTTP 200). The frontend should always check `succeeded`.
- Authentication failures are GraphQL errors (HTTP 401 via JWT).
- Unexpected exceptions are mapped to `INTERNAL_SERVER_ERROR` by the global
  `GraphQLErrorFilter` (`GraphQL/Errors/GraphQLErrorFilter.cs`).

---

## 8. GraphQL API — Subscriptions

Real-time events over WebSocket at `/gql`. **Requires JWT** (sent as
`Authorization` header or `access_token` query param).

Each subscription takes the id of the **affected user or group**, matching the topic
the backend publishes to:

| Subscription | Argument | Payload | Published on |
|---|---|---|---|
| `incomingCall` | `userId: UUID!` | `VideoCallDto` | a call rings `userId` |
| `callAccepted` | `userId: UUID!` | `VideoCallDto` | a call started by `userId` is accepted |
| `callRejected` | `userId: UUID!` | `VideoCallDto` | a call started by `userId` is rejected |
| `callEnded` | `userId: UUID!` | `VideoCallDto` | a call `userId` is in ends |
| `callMissed` | `userId: UUID!` | `VideoCallDto` | a call started by `userId` was missed |
| `groupMessageSent` | `groupId: UUID!` | `GroupMessageDto` | a message is sent in `groupId` |
| `groupCallStarted` | `groupId: UUID!` | `GroupCallDto` | a group call starts |
| `groupCallEnded` | `groupId: UUID!` | `GroupCallDto` | a group call ends |

Example:

```graphql
subscription {
  incomingCall(userId: "00000000-0000-0000-0000-000000000000") {
    callId roomName callerName status
  }
}
```

---

## 9. Call lifecycle, business rules & background jobs

### 1-to-1 call flow

1. `startVideoCall` creates a private Daily room (`max_participants: 2`, 30-min expiry),
   issues an owner token for the caller, persists the call as `RINGING`, sends a web
   push to the recipient and publishes `incomingCall`.
2. `acceptVideoCall` (recipient only) issues a non-owner token and sets `ACCEPTED`;
   the caller gets `callAccepted`.
3. The webhook fires `participant.joined` → the call becomes `CONNECTED`.
4. `endVideoCall` (either side) deletes the room and sets `ENDED`; the other side gets
   `callEnded`. The webhook also ends the call when the room becomes empty.
5. `rejectVideoCall` (recipient only) sets `REJECTED`; caller gets `callRejected`.
6. A call left `RINGING` for more than **60 seconds** is auto-marked `MISSED` by the
   cleanup service (push `call_missed` + realtime `callMissed`).

Guards: you cannot call yourself, call someone already in a call, or start a call while
you are already in one.

### Group call flow

1. `startGroupCall` (any member) creates a room sized for the group, persists `RINGING`,
   pushes to every other member and publishes `groupCallStarted`.
2. `joinGroupCall` issues a token and adds/updates the participant row; the first join
   moves the call to `CONNECTED`.
3. `endGroupCall` (any member) deletes the room, nulls all participant tokens and
   publishes `groupCallEnded`.
4. Webhook ends the call when the room is empty; the cleanup service ends calls still
   `RINGING` after **5 minutes** and cleans rooms 30 minutes after they end.

### Group permissions

| Action | Owner | Admin | Member |
|---|---|---|---|
| Update group | ✅ | ✅ | ❌ |
| Delete group | ✅ | ❌ | ❌ |
| Add member (friends only) | ✅ | ✅ | ❌ |
| Remove member | ✅ | ✅ (only regular members) | ❌ |
| Promote / demote admin | ✅ | ❌ | ❌ |
| Send message | ✅ | ✅ | ✅ |
| Leave group | ❌ (delete instead) | ✅ | ✅ |

Enforced in `Services/Groups/GroupPermissions.cs` (pure functions, unit-testable).

### Daily webhook

`POST /api/daily/webhook` (anonymous, no secret validation — it only ever mutates rows
that match a known `roomName`). Handled events: `participant.joined`, `participant.left`,
`room.finished`, `call.connected`, `call-ended`.

### Background cleanup (`DailyRoomCleanupService`)

Runs every 60 seconds and performs: unanswered calls → `MISSED` (+ push), stale finished
rooms deleted, group calls still ringing → `ENDED`, participant tokens cleared.

### Call history — permanent REST records (`/api/call-history`)

Every direct/group call writes a `CallHistory` row the moment it starts (RINGING) and a
single terminal transition when it ends. The record is **permanent** — deleting the
Daily room on call end never deletes it; only the user can. All endpoints require a JWT
and are scoped to records the user belongs to (caller, recipient, or group member).

| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/call-history` | Paginated, newest first. Query: `page` (default 1), `pageSize` (default 20, max 100), `status`, `callType`, `from`, `to`, `search` (caller/recipient/group name). Returns `{ items, page, pageSize, totalPages, totalItems, hasPreviousPage, hasNextPage }`. |
| `GET` | `/api/call-history/{id}` | One record incl. participants; `404` if missing or not visible. |
| `DELETE` | `/api/call-history/{id}` | Delete one record (`404` if missing/not visible). |
| `DELETE` | `/api/call-history` | Delete all the caller's visible history. |

Terminal mappings: answered direct call → `Completed` (duration = ended − answered);
unanswered direct call → `Cancelled`; rejected → `Rejected`; ringing > 60s → `Missed`;
group call → `Completed` if anyone besides the caller joined, else `Cancelled` (duration
= ended − answered, per-participant duration = left − joined).

---

## 10. Standard prompt for a UI/UX developer

> Copy everything below into a ticket/message for the frontend engineer.

---

**Feature: Realtime video calls, group chat and web push notifications.**

**Stack:** React (Vite) + Apollo Client (GraphQL + subscriptions over WebSocket),
Daily Prebuilt for the media UI, and the Web Push API for notifications. Backend is
already implemented and live at `POST /gql` (GraphQL, incl. subscriptions via WebSocket).
All requests require `Authorization: Bearer <jwt>`.

### Scope

Build, as a polished mobile-first UI:

1. **Call screen (1-to-1)** — incoming-call overlay with caller avatar/name, answer and
   decline buttons; an in-call screen embedding Daily Prebuilt; end-call button; a
   "missed call" toast when the caller is notified.
2. **Group chat** — list of my groups, group detail with members and messages, create
   group modal, send message, member management (add/remove, promote/demote) respecting
   roles.
3. **Group video call** — "Start call" button on a group, join-screen embedding Daily
   Prebuilt, end call.
4. **Push notifications** — one-time browser permission request; register/unregister the
   subscription; show notifications for incoming calls, missed calls and group calls.
5. **Calls history** — a WhatsApp/Discord-style "Calls" tab: paginated list of past calls
   (missed/rejected/completed/cancelled, direct and group), with caller/group avatar,
   name, date, duration and an "incoming/outgoing" indicator; tap to view a call detail
   (participants for group calls); swipe/button to delete one entry or clear all.

### API contract (implement exactly this)

All payloads are wrapped: `{ succeeded, data, message, errors }` — always check `succeeded`.

**Queries**
```graphql
videoCall(callId: UUID!)            # 1-to-1 call state
groups / group(id) / groupMembers   # group chat reads
groupMessages(groupId: UUID!)       # ordered by createdAt
groupCall(callId: UUID!)            # group call state
```

**Mutations**
```graphql
startVideoCall(recipientId: UUID!)  acceptVideoCall(callId: UUID!)
rejectVideoCall(callId: UUID!)      endVideoCall(callId: UUID!)
videoCallToken(callId: UUID!)       # refresh token

registerPushSubscription(endpoint: String!, p256dh: String!, auth: String!)
unregisterPushSubscription(endpoint: String!)

createGroup(name: String!, imageUrl: String)
updateGroup(groupId: UUID!, name: String!, imageUrl: String)
deleteGroup(groupId: UUID!)
addGroupMember(groupId: UUID!, userId: UUID!)   # friends only
removeGroupMember(groupId: UUID!, userId: UUID!)
leaveGroup(groupId: UUID!)
promoteGroupAdmin(groupId: UUID!, userId: UUID!)
demoteGroupAdmin(groupId: UUID!, userId: UUID!)
sendGroupMessage(groupId: UUID!, text: String!)

startGroupCall(groupId: UUID!)  joinGroupCall(callId: UUID!)
endGroupCall(callId: UUID!)     groupCallToken(callId: UUID!)
```

**Call history (plain REST, JWT bearer auth)**
```text
GET    /api/call-history?page&pageSize&status&callType&from&to&search
GET    /api/call-history/{id}
DELETE /api/call-history/{id}
DELETE /api/call-history
```

**Key payload fields**
- `VideoCallDto`: `callId, roomName, roomUrl, token, callerId, callerName, callerAvatar,
  recipientId, status` (RINGING/ACCEPTED/CONNECTED/REJECTED/ENDED/MISSED), `createdAt, endedAt`.
- `GroupCallDto`: `callId, groupId, groupName, roomName, roomUrl, token, startedBy,
  startedByName, status, createdAt, endedAt`.
- `GroupMessageDto`: `id, groupId, senderId, senderName, senderAvatar, text, createdAt, editedAt, deleted`.
- `GroupMemberDto`: `id, groupId, userId, username, fullName, avatar, role` (`"Owner"|"Admin"|"Member"`), `joinedAt`.
- `GroupDto`: `id, name, imageUrl, createdBy, createdByName, createdAt, memberCount`.

**Subscriptions (WebSocket — call during app bootstrap for the logged-in user id / each open group)**
```graphql
incomingCall(userId)   callAccepted(userId)   callRejected(userId)
callEnded(userId)      callMissed(userId)
groupMessageSent(groupId)   groupCallStarted(groupId)   groupCallEnded(groupId)
```

### Behaviour requirements

- **Joining media:** open the `roomUrl` in a Daily Prebuilt iframe and pass the
  `token` returned by `acceptVideoCall` / `joinGroupCall` / `videoCallToken` /
  `groupCallToken`. Never create rooms or tokens client-side.
- **Incoming call UX:** when `incomingCall` fires, show a full-screen ring overlay
  immediately (even if the app is in another tab, rely on web push to re-engage).
  Accept → call `acceptVideoCall`; Decline → `rejectVideoCall`.
- **Missed calls:** surface a toast/notification (payload has `callerName`) and offer a
  "call back" action.
- **Push registration:** after permission is granted, call
  `registerPushSubscription` with the browser's `PushSubscription` values
  (`endpoint`, `p256dh`, `auth`); call `unregisterPushSubscription` on logout. Use the
  backend-exposed VAPID **public** key as `applicationServerKey`.
- **Role-aware UI:** hide group-admin actions (add/remove/promote/demote, delete) unless
  the user's membership role permits them. Owner cannot leave a group.
- **State recovery:** on app load, refetch `groups` and any active call via
  `videoCall`/`groupCall`; re-issue a token with `videoCallToken`/`groupCallToken` if a
  previous one expired.
- **Call history:** load the first page from `GET /api/call-history` on the Calls tab;
  use `isIncoming` for the arrow/icon, `status` for the badge (missed highlighted),
  `durationSeconds` for length and `callType` for the direct/group glyph. Infinite scroll
  via `page`/`pageSize` (last page when `hasNextPage` is false).
- **Error handling:** if `succeeded` is false, show `message`; do not treat these as
  network errors. Handle 401 (expired session) with a global re-auth flow.

### Design guidelines

- Mobile-first, matching the existing brand; light and dark themes.
- Call screen must work in landscape; keep Daily's UI unobtrusive (e.g. small
  "picture-in-picture"-style floating tiles, bottom control bar).
- All interactive states need loading, disabled and empty states; optimistic updates
  for messages where safe.
- Provide a skeleton/call-to-action empty state for "no groups yet".

### Acceptance criteria

1. A user can call a friend, the friend sees an incoming-call overlay, and both enter a
   working Daily room with the backend-provided URL/token.
2. Reject/end/missed flows update both sides in real time (subscription + push).
3. Groups: create, add/remove members, promote/demote admins, send/receive messages live.
4. Group video call starts and notifies every member; join works; end cleans up.
5. Push notifications arrive when the app is in the background for incoming/missed/group calls.
6. All UI respects the permission matrix (owner/admin/member).
7. The Calls tab lists past calls with correct incoming/outgoing and status badges,
   supports pagination, and deleting an entry/clearing all updates the list and the backend.
