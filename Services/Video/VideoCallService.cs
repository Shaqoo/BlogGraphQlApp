using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.History;
using BlogGraphQlApp.Services.Daily;
using BlogGraphQlApp.Services.Push;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Video
{
    public class VideoCallService : IVideoCallService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDailyCallService _daily;
        private readonly IWebPushService _push;
        private readonly ICallHistoryService _history;
        private readonly ITopicEventSender _eventSender;
        private readonly ILogger<VideoCallService> _logger;

        public VideoCallService(
            IUnitOfWork unitOfWork,
            IDailyCallService daily,
            IWebPushService push,
            ICallHistoryService history,
            ITopicEventSender eventSender,
            ILogger<VideoCallService> logger)
        {
            _unitOfWork = unitOfWork;
            _daily = daily;
            _push = push;
            _history = history;
            _eventSender = eventSender;
            _logger = logger;
        }

        public async Task<ApiResponse<VideoCallDto>> StartAsync(Guid callerId, Guid recipientId, CallMediaType mediaType, CancellationToken cancellationToken = default)
        {
            if (callerId == recipientId)
                return ApiResponse<VideoCallDto>.Fail("You cannot call yourself.");

            var recipient = await _unitOfWork.Users.GetByIdAsync(recipientId);
            if (recipient is null)
                return ApiResponse<VideoCallDto>.Fail("Recipient not found.");

            var caller = await _unitOfWork.Users.GetByIdAsync(callerId);
            if (caller is null)
                return ApiResponse<VideoCallDto>.Fail("Caller not found.");

            if (await HasActiveCallAsync(callerId))
                return ApiResponse<VideoCallDto>.Fail("You are already in a call.");

            if (await HasActiveCallAsync(recipientId))
                return ApiResponse<VideoCallDto>.Fail("Recipient is already in a call.");

            var callId = Guid.NewGuid();
            var roomName = $"reelio_{callId:N}";
            var expiresAt = DailyCallService.DefaultExpiration();

            try
            {
                var room = await _daily.CreateRoomAsync(roomName, expiresAt, 2, cancellationToken, audioOnly: mediaType == CallMediaType.Voice);
                var callerToken = await _daily.CreateMeetingTokenAsync(roomName, caller.FullName, isOwner: true, expiresAt, cancellationToken);

                var call = new ActiveVideoCall
                {
                    CallId = callId,
                    RoomName = roomName,
                    DailyRoomUrl = room.Url,
                    CallerId = callerId,
                    RecipientId = recipientId,
                    MediaType = mediaType,
                    Status = VideoCallStatus.Ringing
                };

                await _unitOfWork.ActiveVideoCalls.AddAsync(call);
                await _unitOfWork.CompleteAsync(cancellationToken);

                await _history.StartDirectAsync(call.CallId, callerId, recipientId, roomName, DateTime.UtcNow, cancellationToken);

                await NotifyIncomingCallAsync(call, caller);

                var dto = Map(call, caller, callerToken);
                await PublishAsync($"{recipientId}_IncomingCall", dto, cancellationToken);
                return ApiResponse<VideoCallDto>.Success(dto, "Call started.");
            }
            catch (DailyApiException ex)
            {
                _logger.LogError(ex, "Failed to start video call for caller {CallerId}.", callerId);
                return ApiResponse<VideoCallDto>.Fail(ex.Message);
            }
        }

        public async Task<ApiResponse<VideoCallDto>> AcceptAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<VideoCallDto>.Fail("Call not found.");

            if (call.RecipientId != userId)
                return ApiResponse<VideoCallDto>.Fail("You are not the recipient of this call.");

            if (call.Status != VideoCallStatus.Ringing)
                return ApiResponse<VideoCallDto>.Fail("This call can no longer be accepted.");

            try
            {
                var recipientToken = await _daily.CreateMeetingTokenAsync(call.RoomName, call.Recipient?.FullName ?? "recipient", isOwner: false, DateTime.UtcNow.AddMinutes(30), cancellationToken);

                call.Status = VideoCallStatus.Accepted;
                _unitOfWork.ActiveVideoCalls.Update(call);
                await _unitOfWork.CompleteAsync(cancellationToken);

                await _history.MarkAnsweredAsync(call.CallId, DateTime.UtcNow, cancellationToken);

                var dto = Map(call, null, recipientToken);
                await PublishAsync($"{call.CallerId}_CallAccepted", dto, cancellationToken);
                return ApiResponse<VideoCallDto>.Success(dto, "Call accepted.");
            }
            catch (DailyApiException ex)
            {
                _logger.LogError(ex, "Failed to accept call {CallId}.", callId);
                return ApiResponse<VideoCallDto>.Fail(ex.Message);
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<bool>.Fail("Call not found.");

            if (call.RecipientId != userId)
                return ApiResponse<bool>.Fail("You are not the recipient of this call.");

            if (call.Status is VideoCallStatus.Rejected or VideoCallStatus.Ended or VideoCallStatus.Missed)
                return ApiResponse<bool>.Success(true, "Call already finished.");

            await _daily.EndRoomAsync(call.RoomName, cancellationToken);

            call.Status = VideoCallStatus.Rejected;
            call.EndedAt = DateTime.UtcNow;
            _unitOfWork.ActiveVideoCalls.Update(call);
            await _unitOfWork.CompleteAsync(cancellationToken);

            await _history.RejectDirectAsync(call.CallId, DateTime.UtcNow, userId, cancellationToken);

            await PublishAsync($"{call.CallerId}_CallRejected", Map(call, null, null), cancellationToken);
            _logger.LogInformation("Call {CallId} rejected by {UserId}.", callId, userId);
            return ApiResponse<bool>.Success(true, "Call rejected.");
        }

        public async Task<ApiResponse<bool>> EndAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<bool>.Fail("Call not found.");

            if (call.CallerId != userId && call.RecipientId != userId)
                return ApiResponse<bool>.Fail("You are not a participant of this call.");

            if (call.Status is VideoCallStatus.Ended or VideoCallStatus.Rejected or VideoCallStatus.Missed)
                return ApiResponse<bool>.Success(true, "Call already finished.");

            await _daily.EndRoomAsync(call.RoomName, cancellationToken);

            call.Status = VideoCallStatus.Ended;
            call.EndedAt = DateTime.UtcNow;
            _unitOfWork.ActiveVideoCalls.Update(call);
            await _unitOfWork.CompleteAsync(cancellationToken);

            await _history.EndDirectAsync(call.CallId, DateTime.UtcNow, userId, cancellationToken);

            var otherParticipantId = call.CallerId == userId ? call.RecipientId : call.CallerId;
            await PublishAsync($"{otherParticipantId}_CallEnded", Map(call, null, null), cancellationToken);
            _logger.LogInformation("Call {CallId} ended by {UserId}.", callId, userId);
            return ApiResponse<bool>.Success(true, "Call ended.");
        }

        public async Task<ApiResponse<VideoCallDto>> GetAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<VideoCallDto>.Fail("Call not found.");

            if (call.CallerId != userId && call.RecipientId != userId)
                return ApiResponse<VideoCallDto>.Fail("You are not a participant of this call.");

            return ApiResponse<VideoCallDto>.Success(Map(call, null, null));
        }

        public async Task<ApiResponse<VideoCallDto>> GetActiveIncomingCallAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await _unitOfWork.ActiveVideoCalls
                .Find(c => c.RecipientId == userId && c.Status == VideoCallStatus.Ringing)
                .OrderByDescending(c => c.CreatedAt)
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .FirstOrDefaultAsync(cancellationToken);

            if (call is null)
                return ApiResponse<VideoCallDto>.Fail("No active incoming call.");

            return ApiResponse<VideoCallDto>.Success(Map(call, null, null));
        }

        public async Task<ApiResponse<VideoCallDto>> GetTokenAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<VideoCallDto>.Fail("Call not found.");

            if (call.CallerId != userId && call.RecipientId != userId)
                return ApiResponse<VideoCallDto>.Fail("You are not a participant of this call.");

            try
            {
                var isOwner = call.CallerId == userId;
                var token = await _daily.CreateMeetingTokenAsync(call.RoomName, isOwner ? call.Caller?.FullName ?? "caller" : call.Recipient?.FullName ?? "recipient", isOwner, DateTime.UtcNow.AddMinutes(30), cancellationToken);
                return ApiResponse<VideoCallDto>.Success(Map(call, null, token));
            }
            catch (DailyApiException ex)
            {
                _logger.LogError(ex, "Failed to issue token for call {CallId}.", callId);
                return ApiResponse<VideoCallDto>.Fail(ex.Message);
            }
        }

        private async Task NotifyIncomingCallAsync(ActiveVideoCall call, User caller)
        {
            var payload = new IncomingCallPushPayload
            {
                CallId = call.CallId,
                RoomName = call.RoomName,
                CallerId = caller.Id,
                CallerName = caller.FullName,
                CallerAvatar = caller.ProfilePictureUrl,
                Url = $"/call/{call.CallId}"
            };

            await _push.SendToUserAsync(call.RecipientId, payload);
        }

        private async Task<bool> HasActiveCallAsync(Guid userId) =>
            await _unitOfWork.ActiveVideoCalls.AnyAsync(c =>
                (c.CallerId == userId || c.RecipientId == userId) &&
                (c.Status == VideoCallStatus.Ringing || c.Status == VideoCallStatus.Accepted || c.Status == VideoCallStatus.Connected));

        private async Task<ActiveVideoCall?> FindCallAsync(Guid callId, CancellationToken cancellationToken) =>
            await _unitOfWork.ActiveVideoCalls
                .Find(c => c.CallId == callId)
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .FirstOrDefaultAsync(cancellationToken);

        private async Task PublishAsync<T>(string topic, T payload, CancellationToken cancellationToken)
        {
            try
            {
                await _eventSender.SendAsync(topic, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event to topic {Topic}.", topic);
            }
        }

        private static VideoCallDto Map(ActiveVideoCall call, User? caller, string? token) => new()
        {
            CallId = call.CallId,
            RoomName = call.RoomName,
            RoomUrl = call.DailyRoomUrl,
            Token = token,
            CallerId = call.CallerId,
            CallerName = caller?.FullName ?? call.Caller?.FullName ?? string.Empty,
            CallerAvatar = caller?.ProfilePictureUrl ?? call.Caller?.ProfilePictureUrl,
            RecipientId = call.RecipientId,
            MediaType = call.MediaType,
            Status = call.Status,
            CreatedAt = call.CreatedAt,
            EndedAt = call.EndedAt
        };
    }
}
