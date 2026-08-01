using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GroupQueries
    {
        [Authorize]
        [GraphQLDescription("Gets all groups the current user is a member of.")]
        public async Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetGroupsAsync(userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a single group the current user is a member of.")]
        public async Task<ApiResponse<GroupDto>> GetGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetGroupAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the members of a group the current user belongs to.")]
        public async Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetGroupMembersAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetMembersAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the messages of a group the current user belongs to.")]
        public async Task<ApiResponse<IEnumerable<GroupMessageDto>>> GetGroupMessagesAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetMessagesAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the state of a group video call the user can join.")]
        public async Task<ApiResponse<GroupCallDto>> GetGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetAsync(callId, userId, cancellationToken);
        }
    }
}
