using BlogGraphQlApp.DTOs;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Subscriptions
{
    [ExtendObjectType("Subscription")]
    public class NotificationSubscription
    {
        [Subscribe(With = nameof(SubscribeToNotificationsAsync))]
        [GraphQLDescription("Subscribes to new notifications for the current user.")]
        public NotificationDto OnNotificationReceived([EventMessage] NotificationDto notification)
        {
            return notification;
        }

        public static async ValueTask<ISourceStream<NotificationDto>> SubscribeToNotificationsAsync(Guid userId,[Service] ITopicEventReceiver eventReceiver)
        {
            return await eventReceiver.SubscribeAsync<NotificationDto>($"{userId}_User_NotificationReceived");
        }

        [Subscribe(With = nameof(SubscribeToNotificationReadAsync))]
        public bool OnNotificationRead([EventMessage] bool isRead)
        {
            return isRead;
        }

        
        public static async ValueTask<ISourceStream<NotificationDto>> SubscribeToNotificationReadAsync(Guid userId, [Service] ITopicEventReceiver eventReceiver)
        {
            return await eventReceiver.SubscribeAsync<NotificationDto>($"{userId}_User_NotificationRead");
        }
    }
}
