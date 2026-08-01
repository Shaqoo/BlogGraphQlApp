using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GroupCallQueries
    {
        [Authorize]
        [GraphQLDescription("Gets active group calls across the current user's groups.")]
        public async Task<ApiResponse<IEnumerable<GroupCallDto>>> GetActiveGroupCallsAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetActiveCallsAsync(userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the participants of a group call.")]
        public async Task<ApiResponse<IEnumerable<GroupCallParticipantDto>>> GetGroupCallParticipantsAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetParticipantsAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the call history for a group.")]
        public async Task<ApiResponse<PaginatedResult<CallHistoryDto>>> GetGroupCallHistoryAsync(
            Guid groupId,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetHistoryAsync(groupId, userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }
    }
}
