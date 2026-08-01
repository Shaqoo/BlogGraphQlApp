# GraphQL Schema — Realtime Features Added

Auto-generated from the running HotChocolate 16 server (`GET /gql`). This document covers only the types/fields added for **Daily.co video calls, web push, and group chat/calls** — existing schema surface is omitted.

Endpoints: GraphQL at `/gql`. All new queries, mutations and subscriptions are `@authorize`d.

## Enums

```graphql
enum VideoCallStatus {
  RINGING
  ACCEPTED
  CONNECTED
  REJECTED
  ENDED
  MISSED
}

enum GroupCallStatus {
  RINGING
  CONNECTED
  ENDED
}
```

`GroupMemberRole` (`OWNER`/`ADMIN`/`MEMBER`) is internal only — exposed in the schema as `role: String!` on `GroupMemberDto`.

## Types

```graphql
"One-time Daily meeting token, returned when accepting/joining a call."
type VideoCallDto {
  token: String
  callId: UUID!
  roomName: String!
  roomUrl: String!
  callerId: UUID!
  callerName: String!
  callerAvatar: String
  recipientId: UUID!
  status: VideoCallStatus!
  createdAt: DateTime!
  endedAt: DateTime
}

type GroupCallDto {
  "One-time Daily meeting token, returned when joining a call."
  token: String
  callId: UUID!
  groupId: UUID!
  groupName: String!
  roomName: String!
  roomUrl: String!
  startedBy: UUID!
  startedByName: String!
  status: GroupCallStatus!
  createdAt: DateTime!
  endedAt: DateTime
}

"A group chat group."
type GroupDto {
  id: UUID!
  name: String!
  imageUrl: String
  createdBy: UUID!
  createdByName: String!
  createdAt: DateTime!
  memberCount: Int!
}

"A member of a group chat group."
type GroupMemberDto {
  id: UUID!
  groupId: UUID!
  userId: UUID!
  username: String!
  fullName: String!
  avatar: String
  role: String!
  joinedAt: DateTime!
}

"A message sent inside a group chat."
type GroupMessageDto {
  id: UUID!
  groupId: UUID!
  senderId: UUID!
  senderName: String!
  senderAvatar: String
  text: String!
  createdAt: DateTime!
  editedAt: DateTime
  deleted: Boolean!
}
```

The push payload DTOs (`IncomingCallPushPayload`, `GroupCallPushPayload`) and `GroupMemberRole` are internal and **not** exposed in the GraphQL schema.

## Response Wrappers

```graphql
type ApiResponseOfVideoCallDto {
  succeeded: Boolean!
  data: VideoCallDto
  message: String
  errors: [String!]!
}

type ApiResponseOfGroupCallDto {
  succeeded: Boolean!
  data: GroupCallDto
  message: String
  errors: [String!]!
}

type ApiResponseOfGroupDto {
  succeeded: Boolean!
  data: GroupDto
  message: String
  errors: [String!]!
}

type ApiResponseOfGroupMessageDto {
  succeeded: Boolean!
  data: GroupMessageDto
  message: String
  errors: [String!]!
}

type ApiResponseOfIEnumerableOfGroupDto {
  succeeded: Boolean!
  data: [GroupDto]
  message: String
  errors: [String!]!
}

type ApiResponseOfIEnumerableOfGroupMemberDto {
  succeeded: Boolean!
  data: [GroupMemberDto]
  message: String
  errors: [String!]!
}

type ApiResponseOfIEnumerableOfGroupMessageDto {
  succeeded: Boolean!
  data: [GroupMessageDto]
  message: String
  errors: [String!]!
}
```

## Query

```graphql
extend type Query @authorize {
  "Gets the current state of a 1-to-1 video call the user is involved in."
  videoCall(callId: UUID!): ApiResponseOfVideoCallDto! @authorize

  "Gets all groups the current user is a member of."
  groups: ApiResponseOfIEnumerableOfGroupDto! @authorize

  "Gets a single group the current user is a member of."
  group(groupId: UUID!): ApiResponseOfGroupDto! @authorize

  "Gets the members of a group the current user belongs to."
  groupMembers(groupId: UUID!): ApiResponseOfIEnumerableOfGroupMemberDto! @authorize

  "Gets the messages of a group the current user belongs to."
  groupMessages(groupId: UUID!): ApiResponseOfIEnumerableOfGroupMessageDto! @authorize

  "Gets the state of a group video call the user can join."
  groupCall(callId: UUID!): ApiResponseOfGroupCallDto! @authorize
}
```

## Mutation

```graphql
extend type Mutation @authorize {
  "Starts a Daily.co 1-to-1 video call with another user. The recipient gets a realtime and web-push notification."
  startVideoCall(recipientId: UUID!): ApiResponseOfVideoCallDto! @authorize

  "Accepts a ringing call and returns the Daily room URL + meeting token."
  acceptVideoCall(callId: UUID!): ApiResponseOfVideoCallDto! @authorize

  "Rejects a ringing call."
  rejectVideoCall(callId: UUID!): ApiResponseOfBoolean! @authorize

  "Ends an ongoing call and deletes the Daily room."
  endVideoCall(callId: UUID!): ApiResponseOfBoolean! @authorize

  "Gets a fresh Daily meeting token for an accepted call."
  videoCallToken(callId: UUID!): ApiResponseOfVideoCallDto! @authorize

  "Registers the browser web-push subscription of the current user."
  registerPushSubscription(endpoint: String! p256dh: String! auth: String!): ApiResponseOfBoolean! @authorize

  "Removes the given web-push subscription of the current user."
  unregisterPushSubscription(endpoint: String!): ApiResponseOfBoolean! @authorize

  "Creates a group chat and adds the creator as owner."
  createGroup(name: String! imageUrl: String): ApiResponseOfGroupDto! @authorize

  "Updates the name/image of a group (owner or admin only)."
  updateGroup(groupId: UUID! name: String! imageUrl: String): ApiResponseOfGroupDto! @authorize

  "Deletes a group (owner only)."
  deleteGroup(groupId: UUID!): ApiResponseOfBoolean! @authorize

  "Adds a user to a group (owner or admin only)."
  addGroupMember(groupId: UUID! userId: UUID!): ApiResponseOfBoolean! @authorize

  "Removes a member from a group (owner or admin only)."
  removeGroupMember(groupId: UUID! userId: UUID!): ApiResponseOfBoolean! @authorize

  "Leaves a group the current user belongs to."
  leaveGroup(groupId: UUID!): ApiResponseOfBoolean! @authorize

  "Promotes a member to admin (owner only)."
  promoteGroupAdmin(groupId: UUID! userId: UUID!): ApiResponseOfBoolean! @authorize

  "Demotes an admin back to member (owner only)."
  demoteGroupAdmin(groupId: UUID! userId: UUID!): ApiResponseOfBoolean! @authorize

  "Sends a message in a group the current user is a member of."
  sendGroupMessage(groupId: UUID! text: String!): ApiResponseOfGroupMessageDto! @authorize

  "Starts a group video call for a group the current user is a member of."
  startGroupCall(groupId: UUID!): ApiResponseOfGroupCallDto! @authorize

  "Joins an active group video call and returns the Daily room URL + meeting token."
  joinGroupCall(callId: UUID!): ApiResponseOfGroupCallDto! @authorize

  "Ends a group video call (any participant can end it)."
  endGroupCall(callId: UUID!): ApiResponseOfBoolean! @authorize

  "Gets a fresh Daily meeting token for an active group call."
  groupCallToken(callId: UUID!): ApiResponseOfGroupCallDto! @authorize
}
```

Note: the legacy Agora `startVideoCall` mutation and its Agora packages have been removed entirely — Daily.co is the only call provider.

## Subscription

```graphql
extend type Subscription @authorize {
  "Receives a realtime notification when a call rings the given user."
  incomingCall(userId: UUID!): VideoCallDto! @authorize

  "Receives a realtime notification when a call the user started is accepted."
  callAccepted(userId: UUID!): VideoCallDto! @authorize

  "Receives a realtime notification when a call the user started is rejected."
  callRejected(userId: UUID!): VideoCallDto! @authorize

  "Receives a realtime notification when a call the user is in ends."
  callEnded(userId: UUID!): VideoCallDto! @authorize

  "Receives a realtime notification when a call the user started was missed."
  callMissed(userId: UUID!): VideoCallDto! @authorize

  "Receives a realtime notification when a new group message is sent."
  groupMessageSent(groupId: UUID!): GroupMessageDto! @authorize

  "Receives a realtime notification when a group video call starts."
  groupCallStarted(groupId: UUID!): GroupCallDto! @authorize

  "Receives a realtime notification when a group video call ends."
  groupCallEnded(groupId: UUID!): GroupCallDto! @authorize
}
```
