using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupCallMutations
    {
        [Authorize]
        [GraphQLDescription("Starts a group video call for a group the current user is a member of.")]
        public async Task<ApiResponse<GroupCallDto>> StartGroupCallAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var startedById = claimsPrincipal.GetUserId();
            return await groupCallService.StartAsync(groupId, startedById, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Joins an active group video call and returns the Daily room URL + meeting token.")]
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
        [GraphQLDescription("Ends a group video call (any participant can end it).")]
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
