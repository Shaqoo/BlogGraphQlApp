using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GroupMessageQueries
    {
        [Authorize]
        [GraphQLDescription("Gets paginated messages for a group, newest first.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetGroupMessagesAsync(
            Guid groupId,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMessagesAsync(groupId, userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a single group message.")]
        public async Task<ApiResponse<GroupMessageDto>> GetGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMessageAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets pinned messages for a group.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetPinnedGroupMessagesAsync(
            Guid groupId,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetPinnedMessagesAsync(groupId, userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Searches group messages with the given filters.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> SearchGroupMessagesAsync(
            Guid groupId,
            GroupMessageSearchInput input,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.SearchAsync(groupId, userId, input, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets only media messages (images, videos, documents, audio) from a group.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetGroupMediaAsync(
            Guid groupId,
            MessageType? mediaType,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMediaAsync(groupId, userId, mediaType, page ?? 1, pageSize ?? 20, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the unread message count for a group.")]
        public async Task<ApiResponse<int>> GetGroupUnreadCountAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetUnreadCountAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the total unread count across all the current user's groups.")]
        public async Task<ApiResponse<int>> GetUnreadGroupCountAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetUnreadGroupCountAsync(userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets group messages that mention the current user.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMyGroupMentionsAsync(
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMyMentionsAsync(userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }
    }
}
