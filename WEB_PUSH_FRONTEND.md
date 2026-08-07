# Web Push Notifications — Frontend Prompt + Full Endpoint Doc

> Hand this to the AI building the frontend. It explains **exactly** what the frontend
> must do for Web Push (which is what makes the recipient of a call get a notification
> even when they are on another tab/browser), and documents every endpoint involved.
>
> **The bug it fixes:** you started a 1-to-1 call and the recipient — in another browser —
> saw nothing until the call was marked missed. That happened because the incoming-call
> signal has two channels and the frontend only implemented the WebSocket one:
>
> | Channel | Scope | Delivers |
> |---|---|---|
> | **GraphQL subscription** `incomingCall` | only while the app tab is **open and connected** | the in-app ring overlay |
> | **Web Push** (this doc) | works even when the app is **backgrounded / another tab / another browser** | a browser notification |
>
> Web Push is the ONLY channel that can reach a user who isn't looking at the app right
> now. It must be implemented, or recipients simply get nothing until the call ends.

---

## Part A — Prompt for the frontend (copy-paste)

> **Stack:** React (Vite) + Apollo Client. Browser Web Push API + a service worker
> (e.g. `vite-plugin-pwa`). Backend is live at `/gql` (GraphQL) and `/api/...` (REST).
> Everything except the VAPID key endpoint requires `Authorization: Bearer <jwt>`.

**Goal:** when a user is not looking at the app (backgrounded tab, different browser,
logged in elsewhere), they must still receive a browser notification for **incoming
1-to-1 calls**, **incoming group calls** and **missed calls**, and be able to answer /
open them from the notification.

### 1. Get the VAPID public key (do this once, at startup)

```ts
// GET /api/web-push/vapid-key  (anonymous)
const { publicKey } = await (await fetch('/api/web-push/vapid-key')).json();
```

It is used as `applicationServerKey` below. Convert it from base64url to a
`Uint8Array` for `pushManager.subscribe`:

```ts
const urlBase64ToUint8Array = (base64: string) => {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const raw = atob((base64 + padding).replace(/-/g, '+').replace(/_/g, '/'));
  return Uint8Array.from([...raw].map((c) => c.charCodeAt(0)));
};
```

### 2. Register the subscription (after login, once per browser)

```ts
// Notification.permission must be 'granted' first (ask on login / first visit).
const registration = await navigator.serviceWorker.ready;
const sub = await registration.pushManager.subscribe({
  userVisibleOnly: true,
  applicationServerKey: urlBase64ToUint8Array(publicKey),
});

// POST to GraphQL:
mutation RegisterPush($endpoint: String!, $p256dh: String!, $auth: String!) {
  registerPushSubscription(endpoint: $endpoint, p256dh: $p256dh, auth: $auth) {
    succeeded message
  }
}
// $endpoint = sub.endpoint, $p256dh = sub.getKey('p256dh'), $auth = sub.getKey('auth')
// (the two keys are ArrayBuffers -> base64url strings)
```

- Re-register (upsert by endpoint) on every login so the server always has a current row.
- On **logout** call `unregisterPushSubscription(endpoint)` and optionally
  `registration.pushManager.unsubscribe()`.

### 3. Service worker: show a notification for each push type

The backend sends JSON with a `type` discriminator. Handle these six types:

```jsonc
// 1-to-1 incoming call
{ "type": "video_call", "callId": "…", "roomName": "…", "callerId": "…",
  "callerName": "…", "callerAvatar": "…", "url": "/call/{callId}" }

// group call
{ "type": "group_call", "callId": "…", "groupId": "…", "groupName": "…",
  "roomName": "…", "startedById": "…", "startedByName": "…", "url": "/call/{callId}" }

// missed call
{ "type": "call_missed", "callId": "…", "roomName": "…", "callerId": "…",
  "callerName": "…", "callerAvatar": "…", "url": "/call/{callId}" }

// new follower
{ "type": "new_follower", "followerId": "…", "followerName": "…",
  "followerAvatar": "…", "url": "/profile/{followerId}" }

// direct message
{ "type": "message", "conversationId": "…", "senderId": "…", "senderName": "…",
  "senderAvatar": "…", "preview": "…", "url": "/messages/{conversationId}" }

// group message
{ "type": "group_message", "groupId": "…", "groupName": "…", "senderId": "…",
  "senderName": "…", "senderAvatar": "…", "preview": "…", "url": "/groups/{groupId}" }
```

```ts
self.addEventListener('push', (event) => {
  const data = event.data.json();
  const { type } = data;
  const url = data.url || '/';

  let title = '';
  let body = '';
  let icon = undefined;

  if (type === 'video_call') {
    title = `${data.callerName} is calling…`;
    body = 'Tap to answer the video call';
    icon = data.callerAvatar || undefined;
  } else if (type === 'group_call') {
    title = `${data.startedByName} started a call in ${data.groupName}`;
    body = 'Tap to join the call';
  } else if (type === 'call_missed') {
    title = 'Missed call';
    body = data.callerName ? `You missed a call from ${data.callerName}` : 'You missed a call';
    icon = data.callerAvatar || undefined;
  } else if (type === 'new_follower') {
    title = `${data.followerName} started following you`;
    body = 'Tap to view their profile';
    icon = data.followerAvatar || undefined;
  } else if (type === 'message') {
    title = data.senderName;
    body = data.preview;
    icon = data.senderAvatar || undefined;
  } else if (type === 'group_message') {
    title = `${data.groupName}`;
    body = `${data.senderName}: ${data.preview}`;
    icon = data.senderAvatar || undefined;
  } else {
    return; // ignore unknown types
  }

  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      icon,
      tag: type === 'video_call' || type === 'group_call' ? `call_${data.callId}` : undefined,
      renotify: type !== 'call_missed',
      data, // carry the whole payload for notificationclick
    })
  );
});
```

### 4. Answer/join from the notification (`notificationclick`)

```ts
self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const data = event.notification.data || {};
  event.waitUntil(clients.openWindow(data.url || '/'));
});
```

`url` is `/call/{callId}` — the frontend route must handle it: query
`videoCall(callId)` / `groupCall(callId)`, and if the call is still `RINGING` as the
recipient, offer Answer/Decline (`acceptVideoCall` / `rejectVideoCall`). For group
calls offer Join (`joinGroupCall`).

### 5. Keep the in-app overlay as well

While the app **is** focused, drive the ring overlay from the GraphQL subscription
(`incomingCall(userId)`), not from push — it is instant and lets the user accept
without leaving the app. Use push as the fallback for the background case only.
Avoid double-ringing: if the subscription event fires, suppress the notification.

---

## Part B — Full endpoint documentation

### B1. `GET /api/web-push/vapid-key` (anonymous)

Returns the VAPID **public** key so the frontend can subscribe. No auth.

```json
{ "publicKey": "BEl4l0hFnz6k…" }
```

| Status | Meaning |
|---|---|
| `200` | `publicKey` is set |
| `503` | Web Push not configured (`.env` lacks `WebPush__PublicKey`) |

### B2. `registerPushSubscription(endpoint, p256dh, auth)` — GraphQL mutation (auth)

Registers (or upserts) the browser's push subscription for the current user.

```graphql
mutation {
  registerPushSubscription(endpoint: String!, p256dh: String!, auth: String!) {
    succeeded: Boolean!
    message: String
    errors: [String!]
  }
}
```

- **endpoint** — the browser push endpoint (`PushSubscription.endpoint`).
- **p256dh** — `subscription.getKey('p256dh')` as a base64url string.
- **auth** — `subscription.getKey('auth')` as a base64url string.
- Storage: `UserWebPushSubscription` table, one row per user+endpoint.
- Errors: `succeeded:false` if the request is malformed (e.g. empty endpoint).

### B3. `unregisterPushSubscription(endpoint)` — GraphQL mutation (auth)

Removes the given subscription for the current user. Call on logout.

```graphql
mutation {
  unregisterPushSubscription(endpoint: String!) {
    succeeded: Boolean!
    message: String
    errors: [String!]
  }
}
```

### B4. Push payloads the service worker will receive

All are JSON in the push body. `url` is always a frontend route the app must handle.

**`video_call`** — sent to the **recipient** when a 1-to-1 call rings them
(`VideoCallService.StartAsync`):
| Field | Type | Notes |
|---|---|---|
| `type` | string | `"video_call"` |
| `callId` | uuid | stable call id |
| `roomName` | string | Daily room (informational) |
| `callerId` | uuid | who is calling |
| `callerName` | string | |
| `callerAvatar` | string? | |
| `url` | string | `/call/{callId}` |

**`group_call`** — sent to **every other member** when a group call starts
(`GroupCallService.NotifyGroupCallAsync`):
| Field | Type | Notes |
|---|---|---|
| `type` | string | `"group_call"` |
| `callId` | uuid | |
| `groupId` | uuid | |
| `groupName` | string | |
| `roomName` | string | |
| `startedById` | uuid | who started it |
| `startedByName` | string | |
| `url` | string | `/call/{callId}` |

**`call_missed`** — sent to the **caller** when their call times out unanswered
(`DailyRoomCleanupService`):
| Field | Type | Notes |
|---|---|---|
| `type` | string | `"call_missed"` |
| `callId` | uuid | |
| `roomName` | string | |
| `callerId` | uuid | |
| `url` | string | `/call/{callId}` |

**`new_follower`** — sent to the **followed user** when someone starts following them
(`UserFollowService.FollowUserAsync`):
| Field | Type | Notes |
|---|---|---|
| `type` | string | `"new_follower"` |
| `followerId` | uuid | who started following |
| `followerName` | string | |
| `followerAvatar` | string? | |
| `url` | string | `/profile/{followerId}` |

**`message`** — sent to the **recipient** of a direct message (`MessagingService.SendMessageAsync`):
| Field | Type | Notes |
|---|---|---|
| `type` | string | `"message"` |
| `conversationId` | uuid | |
| `senderId` | uuid | who sent the message |
| `senderName` | string | |
| `senderAvatar` | string? | |
| `preview` | string | text content, or the media type (e.g. `Image`) |
| `url` | string | `/messages/{conversationId}` |

**`group_message`** — sent to **every member with notification level `All`** (non-muted)
except the sender when a message is posted (`GroupMessageService.SendAsync`):
| Field | Type | Notes |
|---|---|---|
| `type` | string | `"group_message"` |
| `groupId` | uuid | |
| `groupName` | string | |
| `senderId` | uuid | |
| `senderName` | string | |
| `senderAvatar` | string? | |
| `preview` | string | text content, or the media type (e.g. `Image`) |
| `url` | string | `/groups/{groupId}` |

> These six are the payloads sent today. The service worker must ignore unknown `type`
> values gracefully. Members with `MentionsOnly`/`Muted` levels do not get general
> group-message pushes (mention/reply notifications are in-app only for now).

### B5. Delivery semantics & limits

- Delivery: RFC 8030 Web Push, VAPID-signed by the backend (`WebPushService`).
- Concurrency capped at **5** simultaneous sends; dead subscriptions (HTTP 404/410)
  are auto-removed from the DB.
- If `.env` lacks the VAPID keys, `SendToUsersAsync` logs a warning and skips — the
  server still works, but no notifications are delivered. (Your recipient-saw-nothing
  symptom is most likely this config, OR the frontend never registered a subscription.)
- The **private** VAPID key and Daily API key are server-side secrets — never send them
  to the frontend, never log them.

### B6. Related endpoints the notification routes depend on

- **GraphQL** (all `@authorize`d): `videoCall(callId)`, `acceptVideoCall(callId)`,
  `rejectVideoCall(callId)`, `endVideoCall(callId)`, `groupCall(callId)`,
  `joinGroupCall(callId)`, `groupCallToken(callId)`. See
  `REALTIME_FEATURES.md` §6–8 for the exact shapes.
- **WebSocket** subscriptions: `incomingCall(userId)`, `callMissed(userId)`,
  `groupCallStarted(groupId)`, `onNotificationReceived(userId)`.
- **REST**: `GET /api/call-history` — to show a "Missed calls" list.

---

## Part C — Debugging the "recipient saw nothing" bug

Work through in this order:

1. **Did the recipient's browser register a subscription?**
   `SELECT COUNT(*) FROM WebPushSubscriptions;` — if 0 rows for that user, the
   frontend never called `registerPushSubscription` (Part A §2), so the server had
   nowhere to push.
2. **Is the VAPID public key fetchable?** `curl http://<host>:5000/api/web-push/vapid-key`
   → must return `{ "publicKey": … }`. If 503, `.env` is missing
   `WebPush__PublicKey`/`PrivateKey`/`Subject`.
3. **Does the service worker handle `push` for `video_call`?** DevTools → Application →
   Service Workers → check the last push was received, and that
   `showNotification` ran for type `video_call` (Part A §3).
4. **Wrong `applicationServerKey`?** If `pushManager.subscribe` throws
   `InvalidStateError`/`AbortError`, the base64url→Uint8Array conversion or the key is
   wrong (Part A §1).
5. **Foreground double-fire?** If the app is open and the subscription event also
   fires, both channels may ring — suppress push when the WS event is received
   (Part A §5).
