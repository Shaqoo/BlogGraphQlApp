using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Events;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Subscriptions
{
    [ExtendObjectType("Subscription")]
    public class CallSubscription
    {
        [Subscribe(With = nameof(SubscribeToIncomingCallAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a call rings the given user.")]
        public VideoCallDto IncomingCall([EventMessage] VideoCallDto call) => call;

        public static async ValueTask<ISourceStream<VideoCallDto>> SubscribeToIncomingCallAsync(
            Guid userId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<VideoCallDto>($"{userId}_IncomingCall");

        [Subscribe(With = nameof(SubscribeToCallAcceptedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a call the user started is accepted.")]
        public VideoCallDto CallAccepted([EventMessage] VideoCallDto call) => call;

        public static async ValueTask<ISourceStream<VideoCallDto>> SubscribeToCallAcceptedAsync(
            Guid userId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<VideoCallDto>($"{userId}_CallAccepted");

        [Subscribe(With = nameof(SubscribeToCallRejectedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a call the user started is rejected.")]
        public VideoCallDto CallRejected([EventMessage] VideoCallDto call) => call;

        public static async ValueTask<ISourceStream<VideoCallDto>> SubscribeToCallRejectedAsync(
            Guid userId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<VideoCallDto>($"{userId}_CallRejected");

        [Subscribe(With = nameof(SubscribeToCallEndedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a call the user is in ends.")]
        public VideoCallDto CallEnded([EventMessage] VideoCallDto call) => call;

        public static async ValueTask<ISourceStream<VideoCallDto>> SubscribeToCallEndedAsync(
            Guid userId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<VideoCallDto>($"{userId}_CallEnded");

        [Subscribe(With = nameof(SubscribeToCallMissedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a call the user started was missed.")]
        public VideoCallDto CallMissed([EventMessage] VideoCallDto call) => call;

        public static async ValueTask<ISourceStream<VideoCallDto>> SubscribeToCallMissedAsync(
            Guid userId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<VideoCallDto>($"{userId}_CallMissed");

        [Subscribe(With = nameof(SubscribeToGroupMessageSentAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a new group message is sent.")]
        public GroupMessageDto GroupMessageSent([EventMessage] GroupMessageDto message) => message;

        public static async ValueTask<ISourceStream<GroupMessageDto>> SubscribeToGroupMessageSentAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMessageDto>($"{groupId}_GroupMessage");

        [Subscribe(With = nameof(SubscribeToGroupCallStartedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a group video call starts.")]
        public GroupCallDto GroupCallStarted([EventMessage] GroupCallDto call) => call;

        public static async ValueTask<ISourceStream<GroupCallDto>> SubscribeToGroupCallStartedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallDto>($"{groupId}_GroupCallStarted");

        [Subscribe(With = nameof(SubscribeToGroupCallEndedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime notification when a group video call ends.")]
        public GroupCallDto GroupCallEnded([EventMessage] GroupCallDto call) => call;

        public static async ValueTask<ISourceStream<GroupCallDto>> SubscribeToGroupCallEndedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallDto>($"{groupId}_GroupCallEnded");

        [Subscribe(With = nameof(SubscribeToGroupMessageEditedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group message is edited.")]
        public GroupMessageDto GroupMessageEdited([EventMessage] GroupMessageDto message) => message;

        public static async ValueTask<ISourceStream<GroupMessageDto>> SubscribeToGroupMessageEditedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMessageDto>($"{groupId}_GroupMessageEdited");

        [Subscribe(With = nameof(SubscribeToGroupMessageDeletedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group message is deleted.")]
        public Guid GroupMessageDeleted([EventMessage] Guid messageId) => messageId;

        public static async ValueTask<ISourceStream<Guid>> SubscribeToGroupMessageDeletedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<Guid>($"{groupId}_GroupMessageDeleted");

        [Subscribe(With = nameof(SubscribeToGroupMessagePinnedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group message is pinned or unpinned.")]
        public GroupMessageDto GroupMessagePinned([EventMessage] GroupMessageDto message) => message;

        public static async ValueTask<ISourceStream<GroupMessageDto>> SubscribeToGroupMessagePinnedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMessageDto>($"{groupId}_GroupMessagePinned");

        [Subscribe(With = nameof(SubscribeToGroupMessageReactionAddedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when someone reacts to a group message.")]
        public Guid GroupMessageReactionAdded([EventMessage] Guid messageId) => messageId;

        public static async ValueTask<ISourceStream<Guid>> SubscribeToGroupMessageReactionAddedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<Guid>($"{groupId}_GroupMessageReactionAdded");

        [Subscribe(With = nameof(SubscribeToGroupMessageReactionRemovedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a reaction is removed from a group message.")]
        public Guid GroupMessageReactionRemoved([EventMessage] Guid messageId) => messageId;

        public static async ValueTask<ISourceStream<Guid>> SubscribeToGroupMessageReactionRemovedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<Guid>($"{groupId}_GroupMessageReactionRemoved");

        [Subscribe(With = nameof(SubscribeToGroupMemberJoinedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a member joins a group.")]
        public GroupMemberDto GroupMemberJoined([EventMessage] GroupMemberDto member) => member;

        public static async ValueTask<ISourceStream<GroupMemberDto>> SubscribeToGroupMemberJoinedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMemberDto>($"{groupId}_GroupMemberJoined");

        [Subscribe(With = nameof(SubscribeToGroupMemberLeftAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a member leaves a group.")]
        public GroupMemberDto GroupMemberLeft([EventMessage] GroupMemberDto member) => member;

        public static async ValueTask<ISourceStream<GroupMemberDto>> SubscribeToGroupMemberLeftAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMemberDto>($"{groupId}_GroupMemberLeft");

        [Subscribe(With = nameof(SubscribeToGroupUpdatedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group is updated.")]
        public GroupDto GroupUpdated([EventMessage] GroupDto group) => group;

        public static async ValueTask<ISourceStream<GroupDto>> SubscribeToGroupUpdatedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupDto>($"{groupId}_GroupUpdated");

        [Subscribe(With = nameof(SubscribeToGroupTypingAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a member starts or stops typing in a group.")]
        public GroupTypingEvent UserTypingInGroup([EventMessage] GroupTypingEvent typingEvent) => typingEvent;

        public static async ValueTask<ISourceStream<GroupTypingEvent>> SubscribeToGroupTypingAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupTypingEvent>($"{groupId}_GroupTyping");

        [Subscribe(With = nameof(SubscribeToGroupCallParticipantJoinedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a participant joins a group call.")]
        public GroupCallParticipantDto GroupCallParticipantJoined([EventMessage] GroupCallParticipantDto participant) => participant;

        public static async ValueTask<ISourceStream<GroupCallParticipantDto>> SubscribeToGroupCallParticipantJoinedAsync(
            Guid callId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallParticipantDto>($"{callId}_GroupCallParticipantJoined");

        [Subscribe(With = nameof(SubscribeToGroupCallParticipantLeftAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a participant leaves a group call.")]
        public GroupCallParticipantDto GroupCallParticipantLeft([EventMessage] GroupCallParticipantDto participant) => participant;

        public static async ValueTask<ISourceStream<GroupCallParticipantDto>> SubscribeToGroupCallParticipantLeftAsync(
            Guid callId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallParticipantDto>($"{callId}_GroupCallParticipantLeft");

        [Subscribe(With = nameof(SubscribeToGroupCallParticipantUpdatedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group call participant's state changes (mute/camera/screenshare/hand).")]
        public GroupCallParticipantDto GroupCallParticipantUpdated([EventMessage] GroupCallParticipantDto participant) => participant;

        public static async ValueTask<ISourceStream<GroupCallParticipantDto>> SubscribeToGroupCallParticipantUpdatedAsync(
            Guid callId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallParticipantDto>($"{callId}_GroupCallParticipantUpdated");
    }
}
