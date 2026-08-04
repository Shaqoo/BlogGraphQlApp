using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.Video;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class VideoCallMutations
    {
        [Authorize]
        [GraphQLDescription("Starts a Daily.co 1-to-1 call (voice or video) with another user. The recipient gets a realtime and web-push notification.")]
        public async Task<ApiResponse<VideoCallDto>> StartVideoCallAsync(
            Guid recipientId,
            CallMediaType mediaType,
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var callerId = claimsPrincipal.GetUserId();
            return await videoCallService.StartAsync(callerId, recipientId, mediaType, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Accepts a ringing call and returns the Daily room URL + meeting token.")]
        public async Task<ApiResponse<VideoCallDto>> AcceptVideoCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await videoCallService.AcceptAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Rejects a ringing call.")]
        public async Task<ApiResponse<bool>> RejectVideoCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await videoCallService.RejectAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Ends an ongoing call and deletes the Daily room.")]
        public async Task<ApiResponse<bool>> EndVideoCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await videoCallService.EndAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a fresh Daily meeting token for an accepted call.")]
        public async Task<ApiResponse<VideoCallDto>> GetVideoCallTokenAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IVideoCallService videoCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await videoCallService.GetTokenAsync(callId, userId, cancellationToken);
        }
    }
}
