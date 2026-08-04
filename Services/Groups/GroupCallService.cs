using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.History;
using BlogGraphQlApp.Services.Daily;
using BlogGraphQlApp.Services.Push;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    public class GroupCallService : IGroupCallService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDailyCallService _daily;
        private readonly IWebPushService _push;
        private readonly ICallHistoryService _history;
        private readonly INotificationService _notificationService;
        private readonly IGroupMessageService _messageService;
        private readonly ITopicEventSender _eventSender;
        private readonly ILogger<GroupCallService> _logger;

        public GroupCallService(
            IUnitOfWork unitOfWork,
            IDailyCallService daily,
            IWebPushService push,
            ICallHistoryService history,
            INotificationService notificationService,
            IGroupMessageService messageService,
            ITopicEventSender eventSender,
            ILogger<GroupCallService> logger)
        {
            _unitOfWork = unitOfWork;
            _daily = daily;
            _push = push;
            _history = history;
            _notificationService = notificationService;
            _messageService = messageService;
            _eventSender = eventSender;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupCallDto>> StartAsync(Guid groupId, Guid startedById, CallMediaType mediaType, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null)
                return ApiResponse<GroupCallDto>.Fail("Group not found.");

            var membership = await GetMembershipAsync(groupId, startedById, ct);
            if (membership is null)
                return ApiResponse<GroupCallDto>.Fail("You are not a member of this group.");

            var hasActiveCall = await _unitOfWork.GroupVideoCalls.AnyAsync(c =>
                c.GroupId == groupId && c.Status == GroupCallStatus.Ringing);
            if (hasActiveCall)
                return ApiResponse<GroupCallDto>.Fail("A group call is already in progress.");

            var callId = Guid.NewGuid();
            var roomName = $"reelio_group_{callId:N}";
            var expiresAt = DailyCallService.DefaultExpiration();

            var memberIds = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            try
            {
                var room = await _daily.CreateRoomAsync(roomName, expiresAt, Math.Max(2, memberIds.Count), ct, audioOnly: mediaType == CallMediaType.Voice);
                var starter = await _unitOfWork.Users.GetByIdAsync(startedById);
                var starterToken = await _daily.CreateMeetingTokenAsync(roomName, starter?.FullName ?? "starter", isOwner: true, expiresAt, ct);

                var call = new GroupVideoCall
                {
                    CallId = callId,
                    GroupId = groupId,
                    RoomName = roomName,
                    DailyRoomUrl = room.Url,
                    StartedBy = startedById,
                    Status = GroupCallStatus.Ringing,
                    MediaType = mediaType
                };

                await _unitOfWork.GroupVideoCalls.AddAsync(call);
                await _unitOfWork.GroupVideoCallParticipants.AddAsync(new GroupVideoCallParticipant
                {
                    CallId = callId,
                    UserId = startedById,
                    Token = null,
                    JoinedAt = DateTime.UtcNow
                });
                await _unitOfWork.CompleteAsync(ct);

                await _history.StartGroupAsync(call.CallId, startedById, groupId, roomName, DateTime.UtcNow, ct);

                var otherMembers = memberIds.Where(id => id != startedById).ToList();
                await NotifyGroupCallAsync(call, group, starter, otherMembers);

                if (group is not null)
                {
                    foreach (var memberId in otherMembers)
                    {
                        await _notificationService.CreateAsync(
                            memberId,
                            NotificationType.GroupCallStarted,
                            $"{starter?.FullName ?? "A member"} started a {(mediaType == CallMediaType.Voice ? "voice" : "video")} call in {group.Name}.",
                            call.CallId,
                            (int)NotificationType.GroupCallStarted,
                            null,
                            ct);
                    }
                    await _messageService.InsertSystemMessageAsync(group, startedById, "Call started.", null, ct);
                }

                var dto = Map(call, group, starter, starterToken);
                await PublishAsync($"{groupId}_GroupCallStarted", dto, ct);
                return ApiResponse<GroupCallDto>.Success(dto, "Group call started.");
            }
            catch (DailyApiException ex)
            {
                _logger.LogError(ex, "Failed to start group call for group {GroupId}.", groupId);
                return ApiResponse<GroupCallDto>.Fail(ex.Message);
            }
        }

        public async Task<ApiResponse<GroupCallDto>> JoinAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<GroupCallDto>.Fail("Group call not found.");

            if (call.Status == GroupCallStatus.Ended)
                return ApiResponse<GroupCallDto>.Fail("This group call has ended.");

            var membership = await GetMembershipAsync(call.GroupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<GroupCallDto>.Fail("You are not a member of this group.");

            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                var token = await _daily.CreateMeetingTokenAsync(call.RoomName, user?.FullName ?? "member", isOwner: false, DateTime.UtcNow.AddMinutes(30), cancellationToken);

                var participant = await _unitOfWork.GroupVideoCallParticipants
                    .Find(p => p.CallId == callId && p.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (participant is null)
                {
                    await _unitOfWork.GroupVideoCallParticipants.AddAsync(new GroupVideoCallParticipant
                    {
                        CallId = callId,
                        UserId = userId,
                        Token = token,
                        JoinedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    participant.Token = token;
                    participant.JoinedAt ??= DateTime.UtcNow;
                    participant.LeftAt = null;
                    _unitOfWork.GroupVideoCallParticipants.Update(participant);
                }

                if (call.Status == GroupCallStatus.Ringing)
                {
                    call.Status = GroupCallStatus.Connected;
                    _unitOfWork.GroupVideoCalls.Update(call);
                }

                await _unitOfWork.CompleteAsync(cancellationToken);

                await _history.AddGroupParticipantAsync(call.CallId, userId, DateTime.UtcNow, cancellationToken);

                return ApiResponse<GroupCallDto>.Success(Map(call, call.Group, null, token), "Joined group call.");
            }
            catch (DailyApiException ex)
            {
                _logger.LogError(ex, "Failed to join group call {CallId}.", callId);
                return ApiResponse<GroupCallDto>.Fail(ex.Message);
            }
        }

        public async Task<ApiResponse<bool>> EndAsync(Guid callId, Guid actorId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<bool>.Fail("Group call not found.");

            if (call.Status == GroupCallStatus.Ended)
                return ApiResponse<bool>.Success(true, "Group call already ended.");

            var membership = await GetMembershipAsync(call.GroupId, actorId, cancellationToken);
            if (membership is null)
                return ApiResponse<bool>.Fail("You are not a member of this group.");

            await FinishCallAsync(call, cancellationToken);
            return ApiResponse<bool>.Success(true, "Group call ended.");
        }

        public async Task<ApiResponse<bool>> LeaveAsync(Guid callId, Guid userId, CancellationToken ct = default)
        {
            var call = await FindCallAsync(callId, ct);
            if (call is null) return ApiResponse<bool>.Fail("Group call not found.");

            var participant = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == callId && p.UserId == userId)
                .FirstOrDefaultAsync(ct);
            if (participant is null) return ApiResponse<bool>.Fail("You are not in this call.");

            participant.LeftAt = DateTime.UtcNow;
            participant.Token = null;
            _unitOfWork.GroupVideoCallParticipants.Update(participant);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{callId}_GroupCallParticipantLeft", await ToParticipantDtoAsync(participant, ct), ct);
            return ApiResponse<bool>.Success(true, "Left the call.");
        }

        public async Task<ApiResponse<bool>> ToggleMuteAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.IsMuted = !p.IsMuted, ct);

        public async Task<ApiResponse<bool>> ToggleCameraAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.CameraEnabled = !p.CameraEnabled, ct);

        public async Task<ApiResponse<bool>> ToggleScreenshareAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.ScreenSharing = !p.ScreenSharing, ct);

        public async Task<ApiResponse<bool>> ToggleHandRaisedAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.HandRaised = !p.HandRaised, ct);

        private async Task<ApiResponse<bool>> ToggleParticipantFlagAsync(Guid callId, Guid userId, Action<GroupVideoCallParticipant> toggle, CancellationToken ct)
        {
            var participant = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == callId && p.UserId == userId)
                .Include(p => p.User)
                .FirstOrDefaultAsync(ct);
            if (participant is null) return ApiResponse<bool>.Fail("You are not in this call.");

            toggle(participant);
            _unitOfWork.GroupVideoCallParticipants.Update(participant);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{callId}_GroupCallParticipantUpdated", await ToParticipantDtoAsync(participant, ct), ct);
            return ApiResponse<bool>.Success(true, "Participant state updated.");
        }

        public async Task<ApiResponse<IEnumerable<GroupCallParticipantDto>>> GetParticipantsAsync(Guid callId, Guid userId, CancellationToken ct = default)
        {
            var call = await FindCallAsync(callId, ct);
            if (call is null) return ApiResponse<IEnumerable<GroupCallParticipantDto>>.Fail("Group call not found.");

            var membership = await GetMembershipAsync(call.GroupId, userId, ct);
            if (membership is null)
                return ApiResponse<IEnumerable<GroupCallParticipantDto>>.Fail("You are not a member of this group.");

            var participants = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == callId)
                .Include(p => p.User)
                .OrderBy(p => p.JoinedAt)
                .ToListAsync(ct);

            var dtos = new List<GroupCallParticipantDto>();
            foreach (var p in participants)
                dtos.Add(await ToParticipantDtoAsync(p, ct));
            return ApiResponse<IEnumerable<GroupCallParticipantDto>>.Success(dtos);
        }

        public async Task<ApiResponse<IEnumerable<GroupCallDto>>> GetActiveCallsAsync(Guid userId, CancellationToken ct = default)
        {
            var groupIds = await _unitOfWork.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Select(m => m.GroupId)
                .ToListAsync(ct);

            var calls = await _unitOfWork.GroupVideoCalls
                .Find(c => groupIds.Contains(c.GroupId) && c.Status != GroupCallStatus.Ended)
                .Include(c => c.Group)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

            var dtos = calls.Select(c => Map(c, c.Group, null, null)).ToList();
            return ApiResponse<IEnumerable<GroupCallDto>>.Success(dtos);
        }

        public async Task<ApiResponse<PaginatedResult<CallHistoryDto>>> GetHistoryAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (await GetMembershipAsync(groupId, userId, ct) is null)
                return ApiResponse<PaginatedResult<CallHistoryDto>>.Fail("You are not a member of this group.");

            var query = new CallHistoryQuery { Page = page, PageSize = pageSize, CallType = CallType.Group };
            var history = await _history.GetHistoryAsync(userId, query, ct);

            var filtered = history.Items.Where(h => h.GroupId == groupId).ToList();
            var filteredTotal = history.Items.Count(h => h.GroupId == groupId);
            return ApiResponse<PaginatedResult<CallHistoryDto>>.Success(
                PaginatedResult<CallHistoryDto>.Create(filtered, page, pageSize, filteredTotal));
        }

        public async Task<ApiResponse<GroupCallDto>> GetAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<GroupCallDto>.Fail("Group call not found.");

            var membership = await GetMembershipAsync(call.GroupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<GroupCallDto>.Fail("You are not a member of this group.");

            return ApiResponse<GroupCallDto>.Success(Map(call, call.Group, null, null));
        }

        public async Task<ApiResponse<GroupCallDto>> GetTokenAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null)
                return ApiResponse<GroupCallDto>.Fail("Group call not found.");

            var membership = await GetMembershipAsync(call.GroupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<GroupCallDto>.Fail("You are not a member of this group.");

            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                var isOwner = call.StartedBy == userId;
                var token = await _daily.CreateMeetingTokenAsync(call.RoomName, user?.FullName ?? "member", isOwner, DateTime.UtcNow.AddMinutes(30), cancellationToken);

                var participant = await _unitOfWork.GroupVideoCallParticipants
                    .Find(p => p.CallId == callId && p.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (participant is null)
                {
                    await _unitOfWork.GroupVideoCallParticipants.AddAsync(new GroupVideoCallParticipant
                    {
                        CallId = callId,
                        UserId = userId,
                        Token = token
                    });
                }
                else
                {
                    participant.Token = token;
                    participant.LeftAt = null;
                    _unitOfWork.GroupVideoCallParticipants.Update(participant);
                }
                await _unitOfWork.CompleteAsync(cancellationToken);

                return ApiResponse<GroupCallDto>.Success(Map(call, call.Group, null, token));
            }
            catch (DailyApiException ex)
            {
                _logger.LogError(ex, "Failed to issue token for group call {CallId}.", callId);
                return ApiResponse<GroupCallDto>.Fail(ex.Message);
            }
        }

        public async Task MarkEndedAsync(Guid callId, CancellationToken cancellationToken = default)
        {
            var call = await FindCallAsync(callId, cancellationToken);
            if (call is null || call.Status == GroupCallStatus.Ended)
                return;

            await FinishCallAsync(call, cancellationToken);
        }

        private async Task FinishCallAsync(GroupVideoCall call, CancellationToken cancellationToken)
        {
            await _daily.EndRoomAsync(call.RoomName, cancellationToken);

            call.Status = GroupCallStatus.Ended;
            call.EndedAt = DateTime.UtcNow;
            _unitOfWork.GroupVideoCalls.Update(call);

            var participants = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == call.Id)
                .ToListAsync(cancellationToken);
            foreach (var participant in participants)
            {
                participant.Token = null;
                participant.LeftAt ??= DateTime.UtcNow;
                _unitOfWork.GroupVideoCallParticipants.Update(participant);
            }

            await _unitOfWork.CompleteAsync(cancellationToken);

            await _history.EndGroupAsync(call.CallId, DateTime.UtcNow, cancellationToken);

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(call.GroupId);
            var memberIds = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == call.GroupId)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);
            var joinedIds = participants.Select(p => p.UserId).ToHashSet();
            var missed = memberIds.Where(id => !joinedIds.Contains(id) && id != call.StartedBy).ToList();
            if (group is not null)
            {
                foreach (var missedId in missed)
                {
                    await _notificationService.CreateAsync(
                        missedId,
                        NotificationType.GroupCallMissed,
                        $"You missed a group call in {group.Name}.",
                        call.CallId,
                        (int)NotificationType.GroupCallMissed,
                        null,
                        cancellationToken);
                }
                await _messageService.InsertSystemMessageAsync(group, call.StartedBy, "Call ended.", null, cancellationToken);
            }

            await PublishAsync($"{call.CallId}_GroupCallEnded", Map(call, call.Group, null, null), cancellationToken);
            await PublishAsync($"{call.GroupId}_GroupCallEnded", Map(call, call.Group, null, null), cancellationToken);
            _logger.LogInformation("Group call {CallId} ended.", call.CallId);
        }

        private async Task NotifyGroupCallAsync(GroupVideoCall call, ChatGroup group, Models.User? starter, List<Guid> memberIds)
        {
            var payload = new GroupCallPushPayload
            {
                CallId = call.CallId,
                GroupId = group.Id,
                GroupName = group.Name,
                RoomName = call.RoomName,
                StartedById = call.StartedBy,
                StartedByName = starter?.FullName ?? string.Empty,
                Url = $"/call/{call.CallId}"
            };

            await _push.SendToUsersAsync(memberIds, payload);
        }

        private async Task<ChatGroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken cancellationToken) =>
            await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId && m.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

        private async Task<GroupVideoCall?> FindCallAsync(Guid callId, CancellationToken cancellationToken) =>
            await _unitOfWork.GroupVideoCalls
                .Find(c => c.CallId == callId)
                .Include(c => c.Group)
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

        private static GroupCallDto Map(GroupVideoCall call, ChatGroup? group, Models.User? starter, string? token) => new()
        {
            CallId = call.CallId,
            GroupId = call.GroupId,
            GroupName = group?.Name ?? string.Empty,
            RoomName = call.RoomName,
            RoomUrl = call.DailyRoomUrl,
            Token = token,
            StartedBy = call.StartedBy,
            StartedByName = starter?.FullName ?? string.Empty,
            Status = call.Status,
            MediaType = call.MediaType,
            CreatedAt = call.CreatedAt,
            EndedAt = call.EndedAt
        };

        private async Task<GroupCallParticipantDto> ToParticipantDtoAsync(GroupVideoCallParticipant participant, CancellationToken ct)
        {
            var user = participant.User ?? await _unitOfWork.Users.GetByIdAsync(participant.UserId);
            return new GroupCallParticipantDto
            {
                Id = participant.Id,
                CallId = participant.CallId,
                UserId = participant.UserId,
                FullName = user?.FullName ?? string.Empty,
                Avatar = user?.ProfilePictureUrl,
                JoinedAt = participant.JoinedAt,
                LeftAt = participant.LeftAt,
                IsMuted = participant.IsMuted,
                CameraEnabled = participant.CameraEnabled,
                ScreenSharing = participant.ScreenSharing,
                HandRaised = participant.HandRaised
            };
        }
    }
}
