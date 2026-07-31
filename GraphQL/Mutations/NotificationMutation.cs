using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class NotificationMutation
    {
        public record MarkNotificationAsReadInput(Guid NotificationId);

        [Authorize]
        [GraphQLDescription("Marks a specific notification as read for the current user.")]
        public async Task<ApiResponse<bool>> MarkNotificationAsReadAsync(
            MarkNotificationAsReadInput input,
            [Service] IAuthService authService,
            [Service] ITopicEventSender eventSender,
            [Service] INotificationService notificationService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            var response = await notificationService.MarkAsReadAsync(input.NotificationId);
            if (response.Succeeded)
            {
                var topic = $"{currentUser.Data!.Id}_User_NotificationRead";
                await eventSender.SendAsync(topic, true);
            }
            return response;
        }
    }
}