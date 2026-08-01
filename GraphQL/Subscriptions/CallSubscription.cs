using BlogGraphQlApp.DTOs;
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
    }
}
