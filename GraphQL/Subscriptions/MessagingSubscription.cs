using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Events;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Subscriptions
{
    [ExtendObjectType("Subscription")]
    public class MessagingSubscription
    {
        [Subscribe(With = nameof(SubscribeToMessagesAsync))]
        public MessageDto OnMessageSent([EventMessage] MessageDto message) => message;

        public static async ValueTask<ISourceStream<MessageDto>> SubscribeToMessagesAsync(Guid conversationId, [Service] ITopicEventReceiver eventReceiver)
        {
            return await eventReceiver.SubscribeAsync<MessageDto>($"{conversationId}_MessageSent");
        }

        [Subscribe(With = nameof(SubscribeToTypingAsync))]
        [Topic]
        public TypingEvent UserTyping([EventMessage] TypingEvent typingEvent) => typingEvent;

        public static async ValueTask<ISourceStream<TypingEvent>> SubscribeToTypingAsync(Guid conversationId, [Service] ITopicEventReceiver eventReceiver)
        {
            return await eventReceiver.SubscribeAsync<TypingEvent>($"{conversationId}_UserTyping");
        }

        [Subscribe(With = nameof(SubscribeToRecordingAsync))]
        [Topic]
        public RecordingEvent UserRecording([EventMessage] RecordingEvent recordingEvent) => recordingEvent;
        public static async ValueTask<ISourceStream<RecordingEvent>> SubscribeToRecordingAsync(Guid conversationId, [Service] ITopicEventReceiver eventReceiver)
        {
            return await eventReceiver.SubscribeAsync<RecordingEvent>($"{conversationId}_UserRecording");
        }

        [Subscribe(With = nameof(SubscribeToMessageReadAsync))]
        [Topic]
        public ReadMessageEvent OnMessageRead([EventMessage] ReadMessageEvent readMessageEvent) => readMessageEvent;

        public static async ValueTask<ISourceStream<ReadMessageEvent>> SubscribeToMessageReadAsync(Guid conversationId, [Service] ITopicEventReceiver eventReceiver)
        {
            return await eventReceiver.SubscribeAsync<ReadMessageEvent>($"{conversationId}_MessageRead");
        }
    }
}