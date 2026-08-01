using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
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
            string? description,
            bool isPrivate,
            int? maxMembers,
            string? imageUrl,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var ownerId = claimsPrincipal.GetUserId();
            return await groupService.CreateGroupAsync(ownerId, name, description, isPrivate, maxMembers, imageUrl, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Updates a group's name, description, privacy, archived flag, or member limit (owner or admin only).")]
        public async Task<ApiResponse<GroupDto>> UpdateGroupAsync(
            Guid groupId,
            string? name,
            string? description,
            bool? isPrivate,
            bool? archived,
            int? maxMembers,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.UpdateGroupAsync(groupId, actorId, name, description, isPrivate, archived, maxMembers, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Uploads a new group image (owner or admin only).")]
        public async Task<ApiResponse<GroupDto>> UploadGroupImageAsync(
            Guid groupId,
            IFile image,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.UploadGroupImageAsync(groupId, actorId, image, cancellationToken);
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
        [GraphQLDescription("Transfers group ownership to another member (owner only).")]
        public async Task<ApiResponse<GroupDto>> TransferGroupOwnershipAsync(
            Guid groupId,
            Guid targetUserId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.TransferOwnershipAsync(groupId, actorId, targetUserId, cancellationToken);
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
        [GraphQLDescription("Generates (or regenerates) the invite code for a group (owner or admin only).")]
        public async Task<ApiResponse<string>> GenerateGroupInviteCodeAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.GenerateInviteCodeAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Revokes the invite code for a group (owner or admin only).")]
        public async Task<ApiResponse<bool>> RevokeGroupInviteCodeAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.RevokeInviteCodeAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Joins a public group using an invite code.")]
        public async Task<ApiResponse<GroupDto>> JoinGroupByInviteAsync(
            string inviteCode,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.JoinByInviteAsync(inviteCode, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Requests to join a private group.")]
        public async Task<ApiResponse<bool>> RequestGroupJoinAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.RequestJoinAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Approves a join request and adds the user (owner or admin only).")]
        public async Task<ApiResponse<bool>> ApproveGroupJoinRequestAsync(
            Guid groupId,
            Guid requestId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.ApproveJoinRequestAsync(groupId, actorId, requestId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Rejects a join request (owner or admin only).")]
        public async Task<ApiResponse<bool>> RejectGroupJoinRequestAsync(
            Guid groupId,
            Guid requestId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.RejectJoinRequestAsync(groupId, actorId, requestId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Mutes a group for the current user (optionally until a date).")]
        public async Task<ApiResponse<bool>> MuteGroupAsync(
            Guid groupId,
            DateTime? mutedUntil,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.MuteGroupAsync(groupId, userId, mutedUntil, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Unmutes a group for the current user.")]
        public async Task<ApiResponse<bool>> UnmuteGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.MuteGroupAsync(groupId, userId, null, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Sets the notification level for the current user in a group.")]
        public async Task<ApiResponse<bool>> SetGroupNotificationLevelAsync(
            Guid groupId,
            NotificationLevel level,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.SetNotificationLevelAsync(groupId, userId, level, cancellationToken);
        }
    }
}
