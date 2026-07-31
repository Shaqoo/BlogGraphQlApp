using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class NotificationQueries
    {
        [Authorize]
        [GraphQLDescription("Gets a specific notification by its ID.")]
        public async Task<ApiResponse<NotificationDto?>> GetNotificationById(
            Guid id,
            [Service] INotificationService notificationService)
        {
            return await notificationService.GetNotificationByIdAsync(id);
        }

        [Authorize]
        [UsePaging(typeof(NotificationTypeGql))]
        [GraphQLDescription("Gets a paginated list of notifications for the current user.")]
        public async Task<IQueryable<NotificationDto>> GetMyNotifications(
            [Service] INotificationService notificationService)
        {
            var response = await notificationService.GetNotificationsForUserAsync();
            return response.Data!;
        }
    }
}