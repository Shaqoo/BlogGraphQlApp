# Frontend / UI-UX Prompt — Group Chat & Calling (BlogGraphQlApp)

> Hand this document to the frontend engineer / UI-UX developer to build the UI for the
> group chat + group calling features. The backend is **already implemented and live**.
> All GraphQL operations are at `POST /gql` (subscriptions over WebSocket at `/gql`),
> and everything requires `Authorization: Bearer <jwt>`.
>
> This is the upgrade surface that came **after** the base realtime features documented
> in `REALTIME_FEATURES.md` (Section 10). Read that file first for the 1-to-1 video call,
> call history tab and web push basics; this doc covers the full **group** surface.

---

**Stack:** React (Vite) + Apollo Client (queries, mutations, `graphql-ws` subscriptions),
Daily Prebuilt (DailyProvider) for call media, Web Push API for background notifications.
Mobile-first, light + dark theme.

**Every response is wrapped:** `{ succeeded, data, message, errors }`. Always check
`succeeded`; if `false`, surface `message` to the user (never treat it as a network error).

---

## Feature list to build

1. **Groups list** — my groups, unread badges, member count, last message preview.
2. **Group detail / chat thread** — messages, members, media, pinned, settings.
3. **Create group** — name, description, private/public, image upload, max members.
4. **Edit group / upload image** — Owner/Admin only.
5. **Group settings** — delete (Owner), transfer ownership (Owner), archive.
6. **Members screen** — list, roles (`Owner`/`Admin`/`Member`), add/remove, promote/demote.
7. **Add member** — pick from friends only.
8. **Invite codes** — generate, show, revoke, copy/share.
9. **Join by invite** — paste a code to join a group.
10. **Join requests** — request to join a private group; Owner/Admin approve/reject.
11. **Leave group** — Owner cannot leave (must delete or transfer first).
12. **Mute / notification level** — `All` / `MentionsOnly` / `Muted`, plus mute-until.
13. **Message composer** — text + file attachments (image/audio/video/document) + reply.
14. **Edit / delete own messages**.
15. **Pin / unpin messages** — pinned banner + pinned list.
16. **Reactions** — emoji picker, add/remove, aggregate display.
17. **Read receipts** — delivered/read/unread counts + per-message status.
18. **Mentions** — `@username` inside messages; a "My mentions" list; highlight.
19. **Typing indicators** — live "X is typing…" via subscription.
20. **Search messages** — text query within a group.
21. **Media gallery** — filter messages by type (image/video/audio/document).
22. **Unread counters** — per-group badge + total across all groups.
23. **System messages** — member joined/left, group renamed, etc. (rendered distinctly).
24. **Group calls** — start (voice/video), join/leave, end; participant tiles with
    mute/camera/screenshare/hand states in real time.
25. **Group call history** — per-group paginated list of past calls.

---

## Screens & components

### Screen 1 — Groups list
- Rows: avatar (or initials), `name`, `memberCount`, last message preview +
  `lastSender`, `lastActivityAt` relative time, `unreadCount` badge, pinned indicator.
- FAB → create-group modal. Pull-to-refresh.
- Empty state: illustration + "No groups yet — create or join one".
- Skeleton loaders on first fetch.

### Screen 2 — Chat thread (`groupId`)
- Header: group avatar/name, member count, call button, overflow menu (settings,
  members, mute, search, media, pinned).
- Message bubbles: sender avatar/name (unless consecutive same sender), timestamp,
  delivery status ticks; `replyToMessage` quoted block; reactions row; pinned badge;
  system messages centered + muted styling.
- Composer: text input, attachment button (image/audio/video/document), emoji/reaction
  button, reply-preview bar with cancel, "X is typing…" line above composer.
- Load older pages on scroll-up (infinite pagination, newest at bottom).

### Screen 3 — Members
- Tabs or single list: all members with role badges; Owner/Admin see manage actions.
- "Add member" opens a friend picker (searchable). Invite code card with copy + revoke.
- Join requests queue (for private groups) with approve/reject.

### Screen 4 — Group settings
- Edit name/description/private/maxMembers; upload image; archive; transfer ownership;
  delete group (danger zone, confirm dialog). Visibility gated by role.

### Screen 5 — Search / Media / Pinned
- Dedicated views filtered from the thread header, each with its own pagination.

### Screen 6 — Group call
- In-call: participant grid of tiles (video or avatar+name), each tile shows
  mute/camera/screenshare/hand badges that update **live** from subscriptions.
- Bottom control bar: mute, camera, screenshare, hand, end call.
- Incoming call overlay for all group members when a call rings the group.

---

## GraphQL contract (implement exactly this)

### Queries

```graphql
groups                                    # ApiResponse<[GroupDto!]>
group(groupId: UUID!)                     # ApiResponse<GroupDto>
groupMembers(groupId: UUID!)              # ApiResponse<[GroupMemberDto!]>
groupInviteCode(groupId: UUID!)           # ApiResponse<String>
pendingGroupJoinRequests(groupId: UUID!)  # ApiResponse<[GroupJoinRequestDto!]>

groupMessages(groupId: UUID!, page: Int = 1, pageSize: Int = 20)   # ApiResponse<Paginated<GroupMessageDto>>
groupMessage(groupId: UUID!, messageId: UUID!)
pinnedGroupMessages(groupId: UUID!, page: Int = 1, pageSize: Int = 20)
searchGroupMessages(groupId: UUID!, input: GroupMessageSearchInput!)   # text, senderId?, from?, to?, messageType?
groupMedia(groupId: UUID!, mediaType: MessageType!, page: Int = 1, pageSize: Int = 20)
groupUnreadCount(groupId: UUID!)          # ApiResponse<Int>
unreadGroupCount()                        # ApiResponse<Int>  (all groups)
myGroupMentions(page: Int = 1, pageSize: Int = 20)   # ApiResponse<Paginated<GroupMessageDto>>

groupCall(callId: UUID!)
activeGroupCalls()                        # ApiResponse<[GroupCallDto!]>
groupCallParticipants(callId: UUID!)      # ApiResponse<[GroupCallParticipantDto!]>
groupCallHistory(groupId: UUID!, page: Int = 1, pageSize: Int = 20)  # ApiResponse<Paginated<CallHistoryDto>>
```

### Mutations — groups

```graphql
createGroup(name: String!, description: String, isPrivate: Boolean!, maxMembers: Int, imageUrl: String): ApiResponse<GroupDto>
updateGroup(groupId: UUID!, name: String, description: String, isPrivate: Boolean, archived: Boolean, maxMembers: Int)
uploadGroupImage(groupId: UUID!, image: Upload!)
deleteGroup(groupId: UUID!): ApiResponse<Boolean>
transferGroupOwnership(groupId: UUID!, targetUserId: UUID!): ApiResponse<GroupDto>

addGroupMember(groupId: UUID!, userId: UUID!): ApiResponse<Boolean>    # friends only
removeGroupMember(groupId: UUID!, userId: UUID!): ApiResponse<Boolean>
leaveGroup(groupId: UUID!): ApiResponse<Boolean>                        # Owner cannot
promoteGroupAdmin(groupId: UUID!, userId: UUID!): ApiResponse<Boolean>  # Owner only
demoteGroupAdmin(groupId: UUID!, userId: UUID!): ApiResponse<Boolean>   # Owner only

generateGroupInviteCode(groupId: UUID!): ApiResponse<String>
revokeGroupInviteCode(groupId: UUID!): ApiResponse<Boolean>
joinGroupByInvite(inviteCode: String!): ApiResponse<GroupDto>

requestGroupJoin(groupId: UUID!): ApiResponse<Boolean>                  # private groups
approveGroupJoinRequest(groupId: UUID!, requestId: UUID!): ApiResponse<Boolean>
rejectGroupJoinRequest(groupId: UUID!, requestId: UUID!): ApiResponse<Boolean>

muteGroup(groupId: UUID!, mutedUntil: DateTime): ApiResponse<Boolean>
unmuteGroup(groupId: UUID!): ApiResponse<Boolean>
setGroupNotificationLevel(groupId: UUID!, level: NotificationLevel!): ApiResponse<Boolean>  # ALL | MENTIONS_ONLY | MUTED
```

### Mutations — messages

```graphql
sendGroupMessage(input: { groupId: UUID!, messageType: MessageType!, content: String, file: Upload, replyToMessageId: UUID }): ApiResponse<GroupMessageDto>
editGroupMessage(groupId: UUID!, messageId: UUID!, content: String!): ApiResponse<GroupMessageDto>
deleteGroupMessage(groupId: UUID!, messageId: UUID!): ApiResponse<Boolean>

pinGroupMessage(groupId: UUID!, messageId: UUID!): ApiResponse<GroupMessageDto>
unpinGroupMessage(groupId: UUID!, messageId: UUID!): ApiResponse<GroupMessageDto>

reactToGroupMessage(groupId: UUID!, messageId: UUID!, emoji: String!): ApiResponse<Boolean>
removeGroupReaction(groupId: UUID!, messageId: UUID!): ApiResponse<Boolean>

markGroupMessageDelivered(groupId: UUID!, messageId: UUID!): ApiResponse<Boolean>
markGroupMessageRead(groupId: UUID!, messageId: UUID!): ApiResponse<Boolean>
markAllGroupMessagesRead(groupId: UUID!): ApiResponse<Boolean>
notifyGroupTyping(groupId: UUID!, isTyping: Boolean!): GroupTypingEvent
```

### Mutations — group calls

```graphql
startGroupCall(groupId: UUID!, mediaType: CallMediaType!): ApiResponse<GroupCallDto>  # VOICE | VIDEO
joinGroupCall(callId: UUID!): ApiResponse<GroupCallDto>        # returns token
leaveGroupCall(callId: UUID!): ApiResponse<Boolean>
endGroupCall(callId: UUID!): ApiResponse<Boolean>              # any member
groupCallToken(callId: UUID!): ApiResponse<GroupCallDto>       # refresh token
toggleGroupCallMute(callId: UUID!): ApiResponse<Boolean>
toggleGroupCallCamera(callId: UUID!): ApiResponse<Boolean>
toggleGroupCallScreenshare(callId: UUID!): ApiResponse<Boolean>
toggleGroupCallHandRaised(callId: UUID!): ApiResponse<Boolean>
```

`MessageType`: `TEXT`, `AUDIO`, `IMAGE`, `DOCUMENT`, `VIDEO`, `SYSTEM`.
`NotificationLevel`: `ALL`, `MENTIONS_ONLY`, `MUTED`. `CallMediaType`: `VOICE`, `VIDEO`.

### Subscriptions (WebSocket — one per open group)

```graphql
groupMessageSent(groupId: UUID!): GroupMessageDto
groupMessageEdited(groupId: UUID!): GroupMessageDto
groupMessageDeleted(groupId: UUID!): Guid          # messageId
groupMessagePinned(groupId: UUID!): GroupMessageDto
groupMessageReactionAdded(groupId: UUID!): Guid    # messageId
groupMessageReactionRemoved(groupId: UUID!): Guid  # messageId

groupMemberJoined(groupId: UUID!): GroupMemberDto
groupMemberLeft(groupId: UUID!): GroupMemberDto
groupUpdated(groupId: UUID!): GroupDto
userTypingInGroup(groupId: UUID!): GroupTypingEvent   # { userId, fullName, groupId, isTyping, timestamp }

groupCallStarted(groupId: UUID!): GroupCallDto
groupCallEnded(groupId: UUID!): GroupCallDto
groupCallParticipantJoined(callId: UUID!): GroupCallParticipantDto
groupCallParticipantLeft(callId: UUID!): GroupCallParticipantDto
groupCallParticipantUpdated(callId: UUID!): GroupCallParticipantDto
```

Also keep the base subscriptions from `REALTIME_FEATURES.md`: `incomingCall(userId)`,
`callAccepted/Rejected/Ended/Missed(userId)`, `onNotificationReceived(userId)`,
`onNotificationRead(userId)`.

---

## Key payload shapes

**GroupDto** — `id, name, description?, imageUrl?, isPrivate, inviteCode?,
lastMessageId?, lastMessage?, lastSender?, lastActivityAt?, updatedAt, archived,
maxMembers?, createdBy, memberCount, unreadCount`

**GroupMemberDto** — `id, groupId, userId, username, fullName, avatar?, role
("Owner"|"Admin"|"Member"), joinedAt`

**GroupMessageDto** — `id, groupId, senderId, messageType, content?, fileUrl?,
replyToMessageId?, createdAt, editedAt?, editedBy?, deleted, isPinned, pinnedAt?,
pinnedBy?, status, deliveredCount, readCount, unreadCount, replyToMessage?, mentions?,
reactions?`

**GroupCallDto** — `callId, groupId, groupName, roomName, roomUrl, token?,
startedBy, startedByName, status, mediaType, createdAt, endedAt?`

**GroupCallParticipantDto** — `id, callId, userId, fullName, avatar?, joinedAt?,
leftAt?, isMuted, cameraEnabled, screenSharing, handRaised`

**GroupTypingEvent** — `{ userId, fullName, groupId, isTyping, timestamp }`

---

## Behaviour requirements

- **Media joining:** open `roomUrl` in Daily Prebuilt (iframe or DailyProvider) and pass
  the `token` from `joinGroupCall` / `groupCallToken`. Never create rooms or tokens
  client-side.
- **Incoming group call:** every member receives `groupCallStarted` (+ web push when the
  app is backgrounded) → full-screen ring overlay with the group name + caller; Join →
  `joinGroupCall`. Live tile updates via the `groupCallParticipant*` subscriptions.
- **Typing:** call `notifyGroupTyping(groupId, isTyping: true)` on input debounce, and
  `false` on send/blur; show the indicator only for 3–5 s after the last event.
- **Read receipts:** call `markGroupMessageDelivered` on arrival and
  `markGroupMessageRead` when the message becomes visible; `markAllGroupMessagesRead`
  when the thread opens. Show ✓✓ style ticks from `status` + counts.
- **Optimistic UI:** send, reactions, pin, mute, and read-marks should update
  optimistically, then reconcile with the subscription/response.
- **Mentions:** autocomplete `@` against `groupMembers`; unresolved mentions fall back to
  the raw text. Highlight mentioned messages in `myGroupMentions`.
- **Realtime merge:** dedupe subscription events against a cache keyed by
  `groupId + messageId`; apply `groupMessageEdited`/`groupMessagePinned`/
  `groupMessageDeleted` to cached threads in place.
- **Unread badges:** refresh `groupUnreadCount`/`unreadGroupCount` on every
  `groupMessageSent` for groups not currently open.
- **Role-aware UI:** Owner sees delete/transfer/promote/demote/revoke-code; Admin sees
  add/remove regular members and approve/reject requests; Member sees messages, leave,
  mute, call. Owner's "Leave" is replaced by "Delete group".
- **State recovery:** on app load refetch `groups`, `activeGroupCalls`, and re-issue a
  token with `groupCallToken` for any live call the user is in.
- **Error handling:** every mutation result carries `message`; show it inline (toast /
  banner) and keep the composer state. 401 → global re-auth flow.

---

## Design guidelines

- Mobile-first; chat thread is the primary surface — keyboard-friendly composer with
  safe-area insets.
- Light + dark themes; avatar initials fallback with brand palette; relative timestamps.
- Loading, disabled, optimistic and empty states for every interactive element.
- System messages visually distinct from user messages (centered, low contrast).
- Call UI in landscape, floating tile mode; Daily controls unobtrusive.

---

## Acceptance criteria

1. Create a public + a private group; image upload works; unread badges track messages.
2. Invite-code join and (for private) request/approve/reject flow work end-to-end.
3. Members can be added (friends only), removed, promoted/demoted; role gating is correct.
4. Messages: text + files + reply + edit + delete + pin + reactions all propagate to
   other members **live** within ~1 s.
5. Mentions, read receipts, typing indicator, search, media gallery and pinned list work.
6. A group call rings every member; joining shows live participant tiles; mute/camera/
   screenshare/hand update live; ending cleans up and writes call history.
7. `succeeded:false` responses are shown, never thrown as network errors.
