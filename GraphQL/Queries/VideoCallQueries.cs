using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Video;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class VideoCallQueries
    {
        [Authorize]
        [GraphQLDescription("Gets the current state of a 1-to-1 video call the user is involved in.")]
        public async Task<ApiResponse<VideoCallDto>> GetVideoCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await videoCallService.GetAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the currently ringing incoming 1-to-1 call for the authenticated user, if any.")]
        public async Task<ApiResponse<VideoCallDto>> GetActiveIncomingCallAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await videoCallService.GetActiveIncomingCallAsync(userId, cancellationToken);
        }
    }
}
