using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupCallMutations
    {
        [Authorize]
        [GraphQLDescription("Starts a group call (voice or video) for a group the current user is a member of.")]
        public async Task<ApiResponse<GroupCallDto>> StartGroupCallAsync(
            Guid groupId,
            CallMediaType mediaType,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var startedById = claimsPrincipal.GetUserId();
            return await groupCallService.StartAsync(groupId, startedById, mediaType, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Joins an active group call and returns the Daily room URL + meeting token.")]
        public async Task<ApiResponse<GroupCallDto>> JoinGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.JoinAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Leaves an active group call.")]
        public async Task<ApiResponse<bool>> LeaveGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.LeaveAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Ends a group call.")]
        public async Task<ApiResponse<bool>> EndGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupCallService.EndAsync(callId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's mute state in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallMuteAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleMuteAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's camera in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallCameraAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleCameraAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's screen sharing in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallScreenshareAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleScreenshareAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's raised hand in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallHandRaisedAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleHandRaisedAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a fresh Daily meeting token for an active group call.")]
        public async Task<ApiResponse<GroupCallDto>> GetGroupCallTokenAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetTokenAsync(callId, userId, cancellationToken);
        }
    }
}
