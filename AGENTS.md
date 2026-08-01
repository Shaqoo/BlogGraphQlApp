# Project instructions for coding agents

## Daily Room Management

- **Never use a permanent or shared Daily room.** A new, cryptographically random room
  name (GUID-based, e.g. `reelio_{callId}` for 1-to-1 and `reelio_group_{callId}` for
  group calls) must be generated for **every** 1-to-1 or group call.
- **Build the meeting URL as `https://{DAILY_SUBDOMAIN}.daily.co/{roomName}`.** The Daily
  REST API normally returns the full URL in the room-creation response; the subdomain is
  used as a fallback (`DailyCallService.BuildMeetingUrl`).
- **The frontend must never create rooms or generate tokens.** The backend is solely
  responsible for creating, managing, and deleting rooms. Meeting tokens are issued
  server-side (`IDailyCallService.CreateMeetingTokenAsync`) and are the only thing a
  client ever receives.
- Rooms must be configured as **private, 30-minute expiry, chat disabled,
  screenshare disabled, recording disabled** (`DailyCallService.CreateRoomAsync`).
- **After a call ends (hang up, reject, timeout, or last participant leaves), immediately
  delete the Daily room through the Daily REST API and update the database status to
  `Ended`** (or `Rejected`/`Missed`).
- **A background cleanup service** (`DailyRoomCleanupService`) must periodically sweep and
  remove any orphaned rooms or stale call records so no temporary rooms remain active.
- Call state may also be reconciled from Daily webhooks (`/api/daily/webhook`) — a room
  that becomes empty ends the call, a participant joining marks it `Connected`.

## Call History

- **Call history is permanent and independent of the Daily room.** Deleting a room
  (on call end) must never delete the `CallHistory` record — history is only removed when
  the user deletes it via the REST API.
- **Only Daily.co is used for calls — never Agora.** No Agora packages, settings or
  code may be reintroduced. The GraphQL mutation is `startVideoCall(recipientId)` (Daily),
  which returns `ApiResponseOfVideoCallDto`.
- Every 1-to-1 and group call must write a `CallHistory` row via
  `ICallHistoryService` (`Services/CallHistory/`): `StartDirectAsync`/`StartGroupAsync`
  at start, `MarkAnsweredAsync` on connect, and exactly one terminal transition —
  `EndDirectAsync` (Completed if answered, else Cancelled), `RejectDirectAsync`,
  `MissDirectAsync` (missed ringing calls), or `EndGroupAsync` (Completed if anyone
  besides the caller joined, else Cancelled). Terminal transitions are guarded by
  `IsFinal` and must be invoked **only** on the call-ending path (never on every webhook
  event).
- Lifecycle hooks live in `VideoCallService`, `GroupCallService`, `DailyWebhookService`
  and `DailyRoomCleanupService`. Prefer idempotent writes; share the scoped
  `IUnitOfWork` so history and call-state changes commit together.
- REST surface: `/api/call-history` (all `.RequireAuthorization()`):
  - `GET /api/call-history?page&pageSize&status&callType&from&to&search` — paginated
    (defaults `page=1`, `pageSize=20`), newest first.
  - `GET /api/call-history/{id}` — one record (404 if missing/not visible).
  - `DELETE /api/call-history/{id}` — one record.
  - `DELETE /api/call-history` — clears all the caller's history.
- **Access**: a user may read/delete only records where they are the caller, the
  recipient, or a member of the group the call happened in. Records are returned
  newest-first; `isIncoming` is derived (Group → `callerId != userId`, Direct →
  `recipientId == userId`).
