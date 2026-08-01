using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupMutations
    {
        [Authorize]
        [GraphQLDescription("Creates a group chat and adds the creator as owner.")]
        public async Task<ApiResponse<GroupDto>> CreateGroupAsync(
            string name,
            string? imageUrl,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var ownerId = claimsPrincipal.GetUserId();
            return await groupService.CreateGroupAsync(ownerId, name, imageUrl, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Updates the name/image of a group (owner or admin only).")]
        public async Task<ApiResponse<GroupDto>> UpdateGroupAsync(
            Guid groupId,
            string name,
            string? imageUrl,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.UpdateGroupAsync(groupId, actorId, name, imageUrl, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Deletes a group (owner only).")]
        public async Task<ApiResponse<bool>> DeleteGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.DeleteGroupAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Adds a user to a group (owner or admin only).")]
        public async Task<ApiResponse<bool>> AddGroupMemberAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.AddMemberAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Removes a member from a group (owner or admin only).")]
        public async Task<ApiResponse<bool>> RemoveGroupMemberAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.RemoveMemberAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Leaves a group the current user belongs to.")]
        public async Task<ApiResponse<bool>> LeaveGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.LeaveGroupAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Promotes a member to admin (owner only).")]
        public async Task<ApiResponse<bool>> PromoteGroupAdminAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.PromoteAdminAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Demotes an admin back to member (owner only).")]
        public async Task<ApiResponse<bool>> DemoteGroupAdminAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.DemoteAdminAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Sends a message in a group the current user is a member of.")]
        public async Task<ApiResponse<GroupMessageDto>> SendGroupMessageAsync(
            Guid groupId,
            string text,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var senderId = claimsPrincipal.GetUserId();
            return await groupService.SendMessageAsync(groupId, senderId, text, cancellationToken);
        }
    }
}
