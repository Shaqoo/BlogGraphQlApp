using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.History;
using BlogGraphQlApp.Services.Daily;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlogGraphQlApp.Services.Daily
{
    /// <summary>
    /// Handles Daily webhook events (participant.joined, participant.left, room.finished).
    ///
    /// The Daily webhook (https://docs.daily.co/reference/rest-api/webhooks) fires without a
    /// user context, so state changes here are performed as the system:
    /// - marks calls as Connected once a participant joins;
    /// - ends a call/session when the room becomes empty.
    /// </summary>
    public class DailyWebhookService
    {
        private readonly IUnitOfWork _uow;
        private readonly IDailyCallService _daily;
        private readonly ICallHistoryService _history;
        private readonly ITopicEventSender _events;
        private readonly ILogger<DailyWebhookService> _logger;

        public DailyWebhookService(
            IUnitOfWork uow,
            IDailyCallService daily,
            ICallHistoryService history,
            ITopicEventSender events,
            ILogger<DailyWebhookService> logger)
        {
            _uow = uow;
            _daily = daily;
            _history = history;
            _events = events;
            _logger = logger;
        }

        public async Task HandleAsync(JsonElement body, CancellationToken cancellationToken)
        {
            var eventName = ReadString(body, "event") ?? ReadString(body, "type");
            var roomName = ExtractRoomName(body);

            if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(roomName))
            {
                _logger.LogDebug("Ignoring Daily webhook without event/room: {Body}", body.GetRawText());
                return;
            }

            // 1-to-1 video call
            var call = await _uow.ActiveVideoCalls
                .Find(c => c.RoomName == roomName)
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .FirstOrDefaultAsync(cancellationToken);

            if (call is not null)
            {
                await HandleVideoCallEventAsync(call, eventName, roomName, cancellationToken);
                return;
            }

            // Group video call
            var groupCall = await _uow.GroupVideoCalls
                .Find(c => c.RoomName == roomName)
                .Include(c => c.Group)
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(cancellationToken);

            if (groupCall is not null)
            {
                await HandleGroupCallEventAsync(groupCall, eventName, roomName, cancellationToken);
                return;
            }

            _logger.LogDebug("Daily webhook {Event} for unknown room {Room}.", eventName, roomName);
        }

        private async Task HandleVideoCallEventAsync(ActiveVideoCall call, string eventName, string roomName, CancellationToken ct)
        {
            var changed = false;

            if (eventName is "participant.joined" or "call.connected" && call.Status == VideoCallStatus.Accepted)
            {
                call.Status = VideoCallStatus.Connected;
                call.ConnectedAt ??= DateTime.UtcNow;
                _uow.ActiveVideoCalls.Update(call);
                changed = true;
                _logger.LogInformation("Call {CallId} connected.", call.CallId);
                await _history.MarkAnsweredAsync(call.CallId, DateTime.UtcNow, ct);
            }

            if (eventName is "participant.left" or "room.finished" or "call-ended" && call.Status != VideoCallStatus.Ended)
            {
                if (await IsRoomEmptyAsync(roomName, ct))
                {
                    await _daily.EndRoomAsync(roomName, ct);
                    call.Status = VideoCallStatus.Ended;
                    call.EndedAt = DateTime.UtcNow;
                    _uow.ActiveVideoCalls.Update(call);
                    changed = true;
                    var dto = ToVideoDto(call);
                    await PublishAsync($"{call.CallerId}_CallEnded", dto, ct);
                    await PublishAsync($"{call.RecipientId}_CallEnded", dto, ct);
                    _logger.LogInformation("Call {CallId} ended because the room is empty.", call.CallId);
                    await _history.EndDirectAsync(call.CallId, DateTime.UtcNow, null, ct);
                }
            }

            if (changed)
                await _uow.CompleteAsync(ct);
        }

        private async Task HandleGroupCallEventAsync(GroupVideoCall call, string eventName, string roomName, CancellationToken ct)
        {
            var changed = false;

            if (eventName is "participant.joined" or "call.connected" && call.Status == GroupCallStatus.Ringing)
            {
                call.Status = GroupCallStatus.Connected;
                _uow.GroupVideoCalls.Update(call);
                changed = true;
            }

            if (eventName is "participant.left" or "room.finished" or "call-ended" && call.Status != GroupCallStatus.Ended)
            {
                if (await IsRoomEmptyAsync(roomName, ct))
                {
                    await _daily.EndRoomAsync(roomName, ct);
                    call.Status = GroupCallStatus.Ended;
                    call.EndedAt = DateTime.UtcNow;
                    _uow.GroupVideoCalls.Update(call);
                    foreach (var participant in call.Participants)
                    {
                        participant.Token = null;
                        participant.LeftAt ??= DateTime.UtcNow;
                        _uow.GroupVideoCallParticipants.Update(participant);
                    }

                    changed = true;
                    var dto = ToGroupDto(call);
                    await PublishAsync($"{call.CallId}_GroupCallEnded", dto, ct);
                    await PublishAsync($"{call.GroupId}_GroupCallEnded", dto, ct);
                    _logger.LogInformation("Group call {CallId} ended because the room is empty.", call.CallId);
                    await _history.EndGroupAsync(call.CallId, DateTime.UtcNow, ct);
                }
            }

            if (changed)
                await _uow.CompleteAsync(ct);
        }

        private async Task<bool> IsRoomEmptyAsync(string roomName, CancellationToken ct)
        {
            try
            {
                var status = await _daily.GetRoomAsync(roomName, ct);
                return status.ParticipantCount == 0;
            }
            catch (DailyApiException)
            {
                // Room already gone -> nothing left in it.
                return true;
            }
        }

        private async Task PublishAsync(string topic, object payload, CancellationToken ct)
        {
            try
            {
                await _events.SendAsync(topic, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish webhook event to topic {Topic}.", topic);
            }
        }

        private static VideoCallDto ToVideoDto(ActiveVideoCall call) => new()
        {
            CallId = call.CallId,
            RoomName = call.RoomName,
            RoomUrl = call.DailyRoomUrl,
            CallerId = call.CallerId,
            CallerName = call.Caller?.FullName ?? string.Empty,
            CallerAvatar = call.Caller?.ProfilePictureUrl,
            RecipientId = call.RecipientId,
            Status = call.Status,
            CreatedAt = call.CreatedAt,
            EndedAt = call.EndedAt
        };

        private static GroupCallDto ToGroupDto(GroupVideoCall call) => new()
        {
            CallId = call.CallId,
            GroupId = call.GroupId,
            GroupName = call.Group?.Name ?? string.Empty,
            RoomName = call.RoomName,
            RoomUrl = call.DailyRoomUrl,
            StartedBy = call.StartedBy,
            Status = call.Status,
            CreatedAt = call.CreatedAt,
            EndedAt = call.EndedAt
        };

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            return null;
        }

        private static string? ExtractRoomName(JsonElement body)
        {
            if (body.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                var roomName = ReadString(payload, "room_name") ?? ReadString(payload, "room") ?? ReadString(payload, "roomName");
                if (!string.IsNullOrWhiteSpace(roomName))
                    return roomName;
            }

            return ReadString(body, "room");
        }
    }
}
