using BlogGraphQlApp.Common;
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
        private readonly ITopicEventSender _eventSender;
        private readonly ILogger<GroupCallService> _logger;

        public GroupCallService(
            IUnitOfWork unitOfWork,
            IDailyCallService daily,
            IWebPushService push,
            ICallHistoryService history,
            ITopicEventSender eventSender,
            ILogger<GroupCallService> logger)
        {
            _unitOfWork = unitOfWork;
            _daily = daily;
            _push = push;
            _history = history;
            _eventSender = eventSender;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupCallDto>> StartAsync(Guid groupId, Guid startedById, CancellationToken cancellationToken = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null)
                return ApiResponse<GroupCallDto>.Fail("Group not found.");

            var membership = await GetMembershipAsync(groupId, startedById, cancellationToken);
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
                .ToListAsync(cancellationToken);

            try
            {
                var room = await _daily.CreateRoomAsync(roomName, expiresAt, Math.Max(2, memberIds.Count), cancellationToken);
                var starter = await _unitOfWork.Users.GetByIdAsync(startedById);
                var starterToken = await _daily.CreateMeetingTokenAsync(roomName, starter?.FullName ?? "starter", isOwner: true, expiresAt, cancellationToken);

                var call = new GroupVideoCall
                {
                    CallId = callId,
                    GroupId = groupId,
                    RoomName = roomName,
                    DailyRoomUrl = room.Url,
                    StartedBy = startedById,
                    Status = GroupCallStatus.Ringing
                };

                await _unitOfWork.GroupVideoCalls.AddAsync(call);
                await _unitOfWork.GroupVideoCallParticipants.AddAsync(new GroupVideoCallParticipant
                {
                    CallId = callId,
                    UserId = startedById,
                    Token = null,
                    JoinedAt = DateTime.UtcNow
                });
                await _unitOfWork.CompleteAsync(cancellationToken);

                await _history.StartGroupAsync(call.CallId, startedById, groupId, roomName, DateTime.UtcNow, cancellationToken);

                var otherMembers = memberIds.Where(id => id != startedById).ToList();
                await NotifyGroupCallAsync(call, group, starter, otherMembers);

                var dto = Map(call, group, starter, starterToken);
                await PublishAsync($"{groupId}_GroupCallStarted", dto, cancellationToken);
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

        private async Task PublishAsync(string topic, object payload, CancellationToken cancellationToken)
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
            CreatedAt = call.CreatedAt,
            EndedAt = call.EndedAt
        };
    }
}
