# Group Chat Upgrade — Design Spec

Date: 2026-08-01
Status: Approved
Scope: Backend (ASP.NET Core + EF Core + HotChocolate GraphQL). No frontend changes, no WebPush changes.

## Goal

Upgrade the existing `ChatGroup` / `GroupMessage` / `GroupVideoCall` implementation so group chats are
feature-complete and consistent with the existing 1:1 messaging experience, following the project's existing
architecture (repositories/UnitOfWork, `ApiResponse<T>`, HotChocolate GraphQL, UploadThing, role-based
permissions). One EF Core migration. The existing push notification code must NOT be modified.

## 1. Architecture

- **`GroupMessageService`** (new, mirrors `Services/Implementations/MessagingService.cs`) — owns all
  message behavior: send (text / media / reply), edit, soft-delete, pin/unpin, reactions, read receipts
  (delivered/read), mentions, search, media gallery, pagination.
- **`GroupService`** (extended) — group CRUD, image upload (UploadThing), membership, roles, invite codes,
  join requests, transfer ownership, mute/settings.
- **`GroupCallService`** (extended) — voice/video calls, join/leave, participant state toggles, call history,
  missed-call notifications.
- **`NotificationService`** (extended) — add `CreateAsync`; writes a `Notification` row and publishes to the
  `{userId}_Notification` GraphQL subscription topic. Real-time delivery reuses the existing GraphQL
  subscription + `PresenceHub`; **WebPush code is reused as-is, never edited**.
- **`MentionParser`** (new static helper) — parses `@username` tokens from message text.
- **`GroupPermissions`** (extended) — the single source of truth for all role rules.
- **`GroupPermissionService`** (new scoped helper) — `EnsureMembershipAsync` / `EnsurePermissionAsync`
  used by all three services so permission checks are never scattered.

## 2. Data model

All changes land in **one** EF Core migration.

### 2.1 ChatGroup (extended)

Add: `Description` (string?), `IsPrivate` (bool, default false), `InviteCode` (string?, unique index),
`LastMessageId` (Guid?, FK to GroupMessage, OnDelete Restrict), `LastActivityAt` (DateTime?),
`UpdatedAt` (DateTime), `Archived` (bool, default false), `MaxMembers` (int?), `RowVersion` (byte[],
`IsRowVersion()` concurrency token).

Indexes: `InviteCode` unique; `LastMessageId` FK.

### 2.2 GroupMessage (extended)

Rename `Text` → `Content` (string?, max 2000). Add:

- `MessageType` (`Enums/MessageType`, incl. new `Video` and `System` values, appended — no reordering).
- `FileUrl` (string?).
- `ReplyToMessageId` (Guid?) + `ReplyToMessage` navigation.
- `EditedBy` (Guid?).
- `IsPinned` (bool, default false), `PinnedAt` (DateTime?), `PinnedBy` (Guid?).
- `Metadata` (string?, JSON).
- `Status` (`Enums/MessageStatus`, default `Sent`).
- `RowVersion` (byte[], `IsRowVersion()`).

Indexes: `(GroupId, CreatedAt)`, `(GroupId, SenderId)`, `(GroupId, IsPinned)`, `(GroupId, MessageType)`,
`ReplyToMessageId`, `SenderId`.

Relationships: reply FK `OnDelete(Restrict)` (a reply must survive the original message being deleted;
only the sender of the reply can delete it). `Group.Sender` `OnDelete(Cascade)` stays.

### 2.3 New: GroupMessageMention

`Id`, `MessageId` (FK cascade), `UserId` (FK cascade), `MentionText` (string), `CreatedAt`.
Unique index `(MessageId, UserId)`; index `UserId`.

### 2.4 New: GroupMessageRead

Per-recipient read tracking with two nullable timestamps so we can distinguish delivered vs read:

`Id`, `MessageId` (FK cascade), `UserId` (FK cascade), `DeliveredAt` (DateTime?), `ReadAt` (DateTime?).
Unique index `(MessageId, UserId)`; index `(UserId, ReadAt)`.

### 2.5 ChatGroupMember (extended — per-member settings)

Fold settings into the membership row (settings only exist for members):

Add `Muted` (bool), `MutedUntil` (DateTime?), `NotificationLevel` (`Enums/NotificationLevel`:
`All`, `MentionsOnly`, `Muted`), `LastReadAt` (DateTime?).

### 2.6 New: GroupJoinRequest

`Id`, `GroupId` (FK cascade), `UserId` (FK cascade), `Status` (`Enums/JoinRequestStatus`:
`Pending`, `Approved`, `Rejected`), `RequestedAt`, `ResolvedAt` (DateTime?), `ResolvedBy` (Guid?).
Unique index `(GroupId, UserId)`; index `(GroupId, Status)`.

### 2.7 Reaction (extended — reused)

Add `GroupMessageId` (Guid?) + `GroupMessage` navigation. FK cascade; unique index `(GroupMessageId, UserId)`.

### 2.8 GroupVideoCall (extended)

Add `MediaType` (`Enums/CallMediaType`: `Voice`, `Video`).

### 2.9 GroupVideoCallParticipant (extended)

Add `IsMuted` (bool, default false), `CameraEnabled` (bool, default false), `ScreenSharing` (bool,
default false), `HandRaised` (bool, default false).

### 2.10 Notification (extended)

Add `RelatedEntityId` (Guid?), `RelatedEntityType` (int), `Metadata` (string?, JSON).

### 2.11 IUnitOfWork (extended)

Add `Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)` implemented via
`_context.Database.BeginTransactionAsync`.

### 2.12 Enums

- `MessageType`: append `Video`, `System` (existing values unchanged: Text=1, Audio=2, Image=3, Document=4).
- New `MessageStatus { Sending, Sent, Delivered, Read, Failed }`.
- New `CallMediaType { Voice, Video }`.
- New `NotificationLevel { All, MentionsOnly, Muted }`.
- New `JoinRequestStatus { Pending, Approved, Rejected }`.
- `NotificationType`: append `GroupMemberAdded`, `GroupMention`, `GroupReply`, `GroupReaction`,
  `GroupCallStarted`, `GroupCallMissed`, `GroupUpdated`, `GroupRoleChanged`, `GroupInvite`.

## 3. Services behavior

### 3.1 GroupMessageService

- **SendAsync(groupId, senderId, messageType, content, IFile?, replyToMessageId?, ct)**:
  1. Validate membership (`EnsurePermissionAsync(CanSendMessage)`).
  2. If `messageType != Text && file != null`, upload via `IFileStorage.UploadAsync(file, messageType + "s")`
     (exactly the 1:1 pattern). Upload errors surface as `ApiResponse.Fail` (no partial row).
  3. Parse mentions from content via `MentionParser`; resolve only to current group members; ignore unknown/non-members.
  4. **In a transaction**: insert `GroupMessage` (`Status = Sent`), insert `GroupMessageMention` rows,
     update `ChatGroup.LastMessageId`, `LastActivityAt`, `UpdatedAt`, create `Notification` rows (mentioned
     users respecting their `NotificationLevel`, reply-to sender if replying to someone else). Commit.
  5. After commit, publish subscription events (`{groupId}_GroupMessage`).
- **EditAsync(groupId, messageId, senderId, newContent, ct)**: sender-only; `MessageType.System` cannot be
  edited; sets `EditedAt`/`EditedBy`; handles `DbUpdateConcurrencyException` → `ApiResponse.Fail`.
  Publish `{groupId}_GroupMessageEdited`.
- **DeleteAsync(groupId, messageId, senderId, ct)**: sender-only soft delete: `Deleted = true`, `Content`
  cleared, `FileUrl` kept nulled. Replies, reactions, and reads are preserved. `System` messages cannot be
  deleted. Publish `{groupId}_GroupMessageDeleted`.
- **PinAsync / UnpinAsync**: admin/owner (`CanPinMessage`); set `IsPinned/PinnedAt/PinnedBy`. Publish
  `{groupId}_GroupMessagePinned`.
- **ReactAsync / RemoveReactionAsync**: extend `ReactionService.CreateReactionAsync` / DTO with
  `GroupMessageId`; unique `(GroupMessageId, UserId)` upsert semantics (re-add overwrites). `System` messages
  cannot be reacted to. Publish `{groupId}_GroupMessageReactionAdded/Removed`.
- **MarkDeliveredAsync / MarkReadAsync / MarkAllReadAsync**: upsert `GroupMessageRead`
  (`DeliveredAt` then `ReadAt`); update `ChatGroupMember.LastReadAt` for MarkAllRead. MarkRead publishes a read event.
- **GetMessagesAsync(groupId, userId, page, pageSize, ct)**: paginated, newest-first, `ProjectTo<GroupMessageDto>`.
- **GetMessageAsync(groupId, messageId, userId, ct)**.
- **GetPinnedMessagesAsync(groupId, userId, page, pageSize, ct)**.
- **SearchAsync(groupId, userId, input, ct)**: filters — `text`, `senderId`, `mentionedUserId`, `pinned`,
  `mediaType`, `dateFrom`, `dateTo`, `hasReactions`, `repliesOnly`, pagination.
- **GetMediaAsync(groupId, userId, mediaType?, page, pageSize, ct)** — gallery over Image/Video/Document/Audio.
- **GetUnreadCountAsync(groupId, userId, ct)**: count of messages with `CreatedAt > ChatGroupMember.LastReadAt`
  and `SenderId != userId`, indexed on `(GroupId, CreatedAt)`.
- **GetUnreadGroupCountAsync(userId, ct)**: single grouped query across the user's groups.
- **GetMyMentionsAsync(userId, page, pageSize, ct)**.
- **SystemMessageAsync(group, groupId, actor, kind, detail, ct)**: internal helper that inserts a
  `MessageType.System` message (content = localized summary, `Metadata` = JSON detail, `SenderId` = actor)
  and updates the group's `LastMessageId/LastActivityAt/UpdatedAt` in the same transaction as its caller.

### 3.2 GroupService (extended)

- **CreateGroupAsync**: add `description`, `isPrivate`, `maxMembers`; generate `InviteCode`.
- **UpdateGroupAsync**: name, description, `IsPrivate`, `MaxMembers`, `Archived`; admin/owner
  (`CanUpdateGroup`); sets `UpdatedAt`; emits a `System` message for name/description/image changes;
  publishes `{groupId}_GroupUpdated`.
- **UploadGroupImageAsync(groupId, actorId, IFile, ct)**: admin/owner; UploadThing upload → set `ImageUrl` →
  `IFileStorage.DeleteAsync(oldUrl)`; emits System message; publishes `groupUpdated`.
- **TransferOwnershipAsync(groupId, actorId, targetUserId, ct)**: owner only; target must be a member; old
  owner → `Member` (or `Admin`), target → `Owner`; emits System message + notification.
- **GenerateInviteCodeAsync / RevokeInviteCodeAsync**: admin/owner; regenerate random code (or null on revoke);
  emits System message.
- **JoinByInviteAsync(inviteCode, userId, ct)**: public groups only (private groups use join requests);
  enforces `MaxMembers`; `ChatGroupMember.LastReadAt` initialized.
- **RequestJoinAsync(groupId, userId, ct)**: private groups only; creates/keeps `Pending` request.
- **ApproveJoinRequestAsync(groupId, actorId, requestId, ct)** / **RejectJoinRequestAsync**: admin/owner;
  approval adds member + System message + notification.
- **AddMemberAsync**: unchanged rules; adds System message + notification; enforces `MaxMembers`; publishes
  `groupMemberJoined`.
- **RemoveMemberAsync / LeaveGroupAsync**: 
  - Owner cannot leave without transferring ownership.
  - If the last member leaves → set `Archived = true`.
  - Emits System message + notifications to remaining members; publishes `groupMemberLeft`.
- **PromoteAdminAsync / DemoteAdminAsync**: existing; add notifications + System message.
- **MuteGroupAsync / UnmuteGroupAsync / SetNotificationLevelAsync**: member-only, updates `ChatGroupMember` settings.
- **GetInviteCodeAsync**: admin/owner only.

### 3.3 GroupCallService (extended)

- **StartAsync(groupId, startedById, mediaType, ct)**: store `MediaType`; create `Notification` rows for all
  eligible members (respecting `NotificationLevel` / mute) + existing WebPush payload (unchanged) + existing
  `{groupId}_GroupCallStarted` subscription; emit `System` message "Call started".
- **JoinAsync / GetAsync / GetTokenAsync**: existing, unchanged semantics.
- **LeaveAsync(callId, userId, ct)**: set `LeftAt`, publish `{callId}_GroupCallParticipantLeft`.
- **EndAsync**: existing + publish missed-call notifications for members who never joined
  (`GroupCallParticipantHistory` / participant rows absent) + `System` message "Call ended".
- **ToggleMuteAsync / ToggleCameraAsync / ToggleScreenshareAsync / ToggleHandRaisedAsync**: participant
  state update + publish `{callId}_GroupCallParticipantUpdated` (payload `GroupCallParticipantDto`).
- **GetActiveCallsAsync(userId, ct)**: active calls across the user's groups.
- **GetCallHistoryAsync(groupId, userId, page, pageSize, ct)**: via existing `ICallHistoryService`.
- **GetCallParticipantsAsync(callId, userId, ct)**: map `GroupVideoCallParticipant` → `GroupCallParticipantDto`.

### 3.4 NotificationService (extended)

- **CreateAsync(userId, type, message, relatedEntityId?, relatedEntityType?, metadata?, ct)**: writes the row
  and publishes to `{userId}_Notification`. Used by all group/call/mention flows. Does NOT touch WebPush.
- Existing get/mark-read methods unchanged.

## 4. GraphQL

### 4.1 Types

- `GroupTypeGql`: add `Description`, `IsPrivate`, `InviteCode` (admin/owner only), `LastActivityAt`,
  `UpdatedAt`, `Archived`, `MaxMembers`, `MemberCount`, `LastMessage` (nested `GroupMessageDto`, eager-loaded),
  `LastSender` (nested `UserDto`), `UnreadCount` (computed via `LastReadAt`).
- `GroupMessageTypeGql`: `MessageType`, `Content`, `FileUrl`, `ReplyToMessageId`, `ReplyToMessage` (resolver),
  `Mentions` (resolver), `Reactions` (resolver), `DeliveredCount`, `ReadCount`, `UnreadCount`, `Status`,
  `IsPinned`, `PinnedAt`, `PinnedBy`, `EditedAt`, `EditedBy`, `Deleted`, `CreatedAt`, `Metadata`.
- New `GroupMentionTypeGql`, `GroupCallParticipantTypeGql`, `GroupJoinRequestTypeGql`.
- `NotificationTypeGql`: add `RelatedEntityId`, `RelatedEntityType`, `Metadata`.

### 4.2 DataLoaders (new, registered in Program.cs)

`ReactionsByGroupMessageIdDataLoader`, `MentionsByGroupMessageIdDataLoader`,
`GroupMessageByIdDataLoader` (reply-to), `ReadsByGroupMessageIdDataLoader`.

### 4.3 Queries (all `[Authorize]`, membership-gated in service)

`groupMessages`, `groupMessage`, `pinnedGroupMessages`, `searchGroupMessages`, `groupMedia`,
`groupUnreadCount`, `unreadGroupCount`, `myGroupMentions`, `groupMembers` (incl. `Online` via
`PresenceTracker.IsOnline` and `LastSeen`), `groupInviteCode`, `pendingGroupJoinRequests`,
`activeGroupCalls`, `groupCallHistory`, `groupCallParticipants`.

### 4.4 Mutations

`createGroup`, `updateGroup`, `uploadGroupImage` (`IFile`), `transferGroupOwnership`,
`generateGroupInviteCode`, `revokeGroupInviteCode`, `joinGroupByInvite`, `requestGroupJoin`,
`approveGroupJoinRequest`, `rejectGroupJoinRequest`, `sendGroupMessage(messageType, content, file?,
replyToMessageId?)`, `editGroupMessage`, `deleteGroupMessage`, `pinGroupMessage`, `unpinGroupMessage`,
`reactToGroupMessage`, `removeGroupReaction`, `markGroupMessageDelivered`, `markGroupMessageRead`,
`markAllGroupMessagesRead`, `addGroupMember`, `removeGroupMember`, `promoteGroupAdmin`, `demoteGroupAdmin`,
`leaveGroup`, `muteGroup`, `unmuteGroup`, `setGroupNotificationLevel`, `startGroupCall(mediaType)`,
`joinGroupCall`, `leaveGroupCall`, `endGroupCall`, `toggleGroupCallMute`, `toggleGroupCallCamera`,
`toggleGroupCallScreenshare`, `toggleGroupCallHandRaised`, `notifyGroupTyping(isTyping)`.

### 4.5 Subscriptions

`groupMessageSent` (exists), `groupMessageEdited`, `groupMessageDeleted`, `groupMessagePinned`,
`groupMessageUnpinned`, `groupMessageReactionAdded`, `groupMessageReactionRemoved`, `groupMemberJoined`,
`groupMemberLeft`, `groupUpdated`, `userStartedTyping`, `userStoppedTyping` (topic `{groupId}_GroupTyping`,
published only to group members), `groupCallStarted` (exists), `groupCallEnded` (exists),
`groupCallParticipantJoined`, `groupCallParticipantLeft`, `groupCallParticipantUpdated`,
`notificationReceived` (register existing `NotificationSubscription`).

## 5. Validation & authorization

- FluentValidation validators mirroring existing ones: `SendGroupMessageValidator`,
  `EditGroupMessageValidator`, `SearchGroupMessagesValidator`, `CreateGroupValidator`.
- Membership gate on every group message / membership / call operation; role gates via `GroupPermissions`.
- `[Authorize]` on every mutation/query/subscription.
- `MessageType.System` messages: cannot be edited, deleted, reacted to, or pinned.

## 6. Performance

- Indexes listed in §2 cover every filter/search path.
- Eager loading + `ProjectTo`; DataLoaders prevent N+1 (reactions, mentions, replies, reads).
- All message/media/search queries use `PaginatedResult<T>` (page/pageSize, default 20, max 100).
- Unread counts computed via indexed `(GroupId, CreatedAt)` + `LastReadAt` — no stored counters.

## 7. Consistency

- Transactions via `IUnitOfWork.BeginTransactionAsync` for: send message, add/remove member, join-request
  approval, invite join, transfer ownership, call start/end, mute changes.
- Subscription events publish **after** commit.
- Optimistic concurrency: `RowVersion` on `ChatGroup` + `GroupMessage`; `DbUpdateConcurrencyException` →
  `ApiResponse.Fail("This item was modified by someone else. Refresh and try again.")`.

## 8. Docs & verification

Update `AGENTS.md`, `GRAPHQL_SCHEMA.md`, `REALTIME_FEATURES.md` after implementation.

Verification: `dotnet build` 0 errors; migration created + applied; live GraphQL smoke tests covering group
lifecycle, media message with mention, delivered/read, reaction, pin, search, gallery, edit, soft-delete,
system messages, mute, typing, private-group request flow, voice-call lifecycle, and missed-call notification.

## 9. Execution phases (single plan)

1. Entities, enums, EF configs, migration, DTOs, `IUnitOfWork.BeginTransactionAsync`.
2. `MentionParser`, `NotificationService.CreateAsync`, `GroupMessageService`, `ReactionService` extension.
3. `GroupService` + `GroupCallService` extensions.
4. GraphQL types/queries/mutations/subscriptions/data loaders + Program.cs wiring.
5. Notifications/events integration, docs, verification.

## Non-goals / constraints

- No frontend changes.
- No WebPush code changes (calls and notifications keep using the existing `IWebPushService` as-is).
- No changes to the 1:1 `Message` flow beyond the shared `MessageType` enum gaining `Video`/`System`.
- `MessageType.System` rows are the audit trail (per user preference); no separate audit table.
