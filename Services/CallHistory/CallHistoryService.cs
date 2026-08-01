using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.History
{
    /// <summary>
    /// Persists a permanent call-history record for every direct and group call.
    /// The temporary Daily room is deleted separately when a call finishes; this
    /// record is never tied to the room's lifecycle and is kept until the user
    /// deletes it.
    /// </summary>
    public class CallHistoryService : ICallHistoryService
    {
        private const int MaxPageSize = 100;

        private readonly IUnitOfWork _uow;
        private readonly ILogger<CallHistoryService> _logger;

        public CallHistoryService(IUnitOfWork uow, ILogger<CallHistoryService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ----- Lifecycle -------------------------------------------------------

        public async Task StartDirectAsync(Guid callId, Guid callerId, Guid recipientId, string roomName, DateTime startedAt, CancellationToken ct)
        {
            var history = new CallHistory
            {
                CallId = callId,
                CallType = CallType.Direct,
                CallerId = callerId,
                RecipientId = recipientId,
                RoomName = roomName,
                StartedAt = startedAt,
                Status = CallHistoryStatus.Ringing
            };
            await _uow.CallHistories.AddAsync(history);
            await _uow.CompleteAsync(ct);
            _logger.LogDebug("Call history created for direct call {CallId}.", callId);
        }

        public async Task StartGroupAsync(Guid callId, Guid callerId, Guid groupId, string roomName, DateTime startedAt, CancellationToken ct)
        {
            var history = new CallHistory
            {
                CallId = callId,
                CallType = CallType.Group,
                CallerId = callerId,
                GroupId = groupId,
                RoomName = roomName,
                StartedAt = startedAt,
                Status = CallHistoryStatus.Ringing
            };
            await _uow.CallHistories.AddAsync(history);
            await _uow.GroupCallParticipantHistories.AddAsync(new GroupCallParticipantHistory
            {
                CallHistoryId = history.Id,
                UserId = callerId,
                JoinedAt = startedAt
            });
            await _uow.CompleteAsync(ct);
            _logger.LogDebug("Call history created for group call {CallId} in group {GroupId}.", callId, groupId);
        }

        public async Task MarkAnsweredAsync(Guid callId, DateTime answeredAt, CancellationToken ct)
        {
            var history = await FindByCallIdAsync(callId, ct);
            if (history is null || history.AnsweredAt is not null)
                return;

            history.Status = CallHistoryStatus.Connected;
            history.AnsweredAt = answeredAt;
            _uow.CallHistories.Update(history);
            await _uow.CompleteAsync(ct);
        }

        public async Task EndDirectAsync(Guid callId, DateTime endedAt, Guid? endedByUserId, CancellationToken ct)
        {
            var history = await FindByCallIdAsync(callId, ct);
            if (history is null || IsFinal(history.Status))
                return;

            var answered = history.AnsweredAt is not null;
            history.Status = answered ? CallHistoryStatus.Completed : CallHistoryStatus.Cancelled;
            history.EndedAt = endedAt;
            history.EndedByUserId = endedByUserId;
            history.DurationSeconds = answered
                ? Math.Max(0, (int)(endedAt - history.AnsweredAt!.Value).TotalSeconds)
                : 0;
            _uow.CallHistories.Update(history);
            await _uow.CompleteAsync(ct);
            _logger.LogInformation("Direct call {CallId} history marked as {Status}.", callId, history.Status);
        }

        public async Task RejectDirectAsync(Guid callId, DateTime endedAt, Guid? endedByUserId, CancellationToken ct)
        {
            var history = await FindByCallIdAsync(callId, ct);
            if (history is null || IsFinal(history.Status))
                return;

            history.Status = CallHistoryStatus.Rejected;
            history.EndedAt = endedAt;
            history.EndedByUserId = endedByUserId;
            history.DurationSeconds = 0;
            _uow.CallHistories.Update(history);
            await _uow.CompleteAsync(ct);
            _logger.LogInformation("Direct call {CallId} history marked as Rejected.", callId);
        }

        public async Task MissDirectAsync(Guid callId, DateTime endedAt, CancellationToken ct)
        {
            var history = await FindByCallIdAsync(callId, ct);
            if (history is null || IsFinal(history.Status))
                return;

            history.Status = CallHistoryStatus.Missed;
            history.EndedAt = endedAt;
            history.DurationSeconds = 0;
            _uow.CallHistories.Update(history);
            await _uow.CompleteAsync(ct);
            _logger.LogInformation("Direct call {CallId} history marked as Missed.", callId);
        }

        public async Task AddGroupParticipantAsync(Guid callId, Guid userId, DateTime? joinedAt, CancellationToken ct)
        {
            var history = await FindByCallIdAsync(callId, ct);
            if (history is null || IsFinal(history.Status))
                return;

            var participant = await _uow.GroupCallParticipantHistories
                .Find(p => p.CallHistoryId == history.Id && p.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (participant is null)
            {
                await _uow.GroupCallParticipantHistories.AddAsync(new GroupCallParticipantHistory
                {
                    CallHistoryId = history.Id,
                    UserId = userId,
                    JoinedAt = joinedAt
                });
            }
            else
            {
                participant.JoinedAt ??= joinedAt;
                participant.LeftAt = null;
                _uow.GroupCallParticipantHistories.Update(participant);
            }

            if (history.Status == CallHistoryStatus.Ringing)
            {
                history.Status = CallHistoryStatus.Connected;
                history.AnsweredAt ??= joinedAt ?? DateTime.UtcNow;
                _uow.CallHistories.Update(history);
            }

            await _uow.CompleteAsync(ct);
        }

        public async Task EndGroupAsync(Guid callId, DateTime endedAt, CancellationToken ct)
        {
            var history = await FindByCallIdAsync(callId, ct);
            if (history is null || IsFinal(history.Status))
                return;

            var participants = await _uow.GroupCallParticipantHistories
                .Find(p => p.CallHistoryId == history.Id)
                .ToListAsync(ct);

            var someoneJoined = participants.Any(p => p.UserId != history.CallerId && p.JoinedAt.HasValue);
            history.Status = someoneJoined ? CallHistoryStatus.Completed : CallHistoryStatus.Cancelled;
            history.EndedAt = endedAt;
            history.DurationSeconds = Math.Max(0, (int)(endedAt - (history.AnsweredAt ?? history.StartedAt)).TotalSeconds);

            foreach (var participant in participants)
            {
                participant.LeftAt ??= endedAt;
                participant.DurationSeconds = participant.JoinedAt.HasValue
                    ? Math.Max(0, (int)(endedAt - participant.JoinedAt.Value).TotalSeconds)
                    : 0;
                _uow.GroupCallParticipantHistories.Update(participant);
            }

            _uow.CallHistories.Update(history);
            await _uow.CompleteAsync(ct);
            _logger.LogInformation("Group call {CallId} history marked as {Status}.", callId, history.Status);
        }

        // ----- Queries ---------------------------------------------------------

        public async Task<PaginatedResult<CallHistoryDto>> GetHistoryAsync(Guid userId, CallHistoryQuery query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            var memberGroupIds = _uow.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Select(m => m.GroupId);

            var baseQuery = _uow.CallHistories.GetAll()
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .Include(c => c.Group)
                .Where(c => c.CallerId == userId ||
                            c.RecipientId == userId ||
                            (c.GroupId != null && memberGroupIds.Contains(c.GroupId.Value)));

            if (query.Status is not null)
                baseQuery = baseQuery.Where(c => c.Status == query.Status);

            if (query.CallType is not null)
                baseQuery = baseQuery.Where(c => c.CallType == query.CallType);

            if (query.From is not null)
                baseQuery = baseQuery.Where(c => c.StartedAt >= query.From.Value);

            if (query.To is not null)
                baseQuery = baseQuery.Where(c => c.StartedAt <= query.To.Value);

            var search = query.Search?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
                baseQuery = baseQuery.Where(c =>
                    c.Caller.FullName.Contains(search) ||
                    (c.RecipientId != null && c.Recipient!.FullName.Contains(search)) ||
                    (c.GroupId != null && c.Group!.Name.Contains(search)));

            var total = await baseQuery.CountAsync(ct);
            var items = await baseQuery
                .OrderByDescending(c => c.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return PaginatedResult<CallHistoryDto>.Create(items.Select(c => Map(c, userId)).ToList(), page, pageSize, total);
        }

        public async Task<CallHistoryDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
        {
            var record = await _uow.CallHistories.GetAll()
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .Include(c => c.Group)
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (record is null || !await CanAccessAsync(userId, record, ct))
                return null;

            return Map(record, userId);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
        {
            var record = await _uow.CallHistories.GetByIdAsync(id);
            if (record is null || !await CanAccessAsync(userId, record, ct))
                return false;

            _uow.CallHistories.Remove(record);
            await _uow.CompleteAsync(ct);
            _logger.LogInformation("Call history {Id} deleted by user {UserId}.", id, userId);
            return true;
        }

        public async Task<int> DeleteAllAsync(Guid userId, CancellationToken ct)
        {
            var memberGroupIds = _uow.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Select(m => m.GroupId);

            var records = await _uow.CallHistories.GetAll()
                .Where(c => c.CallerId == userId ||
                            c.RecipientId == userId ||
                            (c.GroupId != null && memberGroupIds.Contains(c.GroupId.Value)))
                .ToListAsync(ct);

            if (records.Count == 0)
                return 0;

            _uow.CallHistories.RemoveRange(records);
            await _uow.CompleteAsync(ct);
            _logger.LogInformation("{Count} call history records deleted by user {UserId}.", records.Count, userId);
            return records.Count;
        }

        // ----- Helpers ---------------------------------------------------------

        private static bool IsFinal(CallHistoryStatus status) =>
            status is CallHistoryStatus.Completed or CallHistoryStatus.Missed or CallHistoryStatus.Rejected or CallHistoryStatus.Cancelled;

        private async Task<CallHistory?> FindByCallIdAsync(Guid callId, CancellationToken ct) =>
            await _uow.CallHistories.Find(c => c.CallId == callId).FirstOrDefaultAsync(ct);

        private async Task<bool> CanAccessAsync(Guid userId, CallHistory record, CancellationToken ct)
        {
            if (record.CallerId == userId || record.RecipientId == userId)
                return true;

            if (record.GroupId is null)
                return false;

            return await _uow.ChatGroupMembers.AnyAsync(m => m.GroupId == record.GroupId.Value && m.UserId == userId);
        }

        private static CallHistoryDto Map(CallHistory c, Guid userId)
        {
            var isIncoming = c.CallType == CallType.Group
                ? c.CallerId != userId
                : c.RecipientId == userId;

            return new CallHistoryDto
            {
                Id = c.Id,
                CallId = c.CallId,
                CallType = c.CallType,
                CallerId = c.CallerId,
                CallerName = c.Caller?.FullName,
                CallerAvatar = c.Caller?.ProfilePictureUrl,
                RecipientId = c.RecipientId,
                RecipientName = c.Recipient?.FullName,
                RecipientAvatar = c.Recipient?.ProfilePictureUrl,
                GroupId = c.GroupId,
                GroupName = c.Group?.Name,
                StartedAt = c.StartedAt,
                AnsweredAt = c.AnsweredAt,
                EndedAt = c.EndedAt,
                DurationSeconds = c.DurationSeconds,
                Status = c.Status,
                EndedByUserId = c.EndedByUserId,
                IsIncoming = isIncoming,
                Participants = c.Participants?
                    .Select(p => new CallHistoryParticipantDto
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Username = p.User?.Username,
                        FullName = p.User?.FullName,
                        Avatar = p.User?.ProfilePictureUrl,
                        JoinedAt = p.JoinedAt,
                        LeftAt = p.LeftAt,
                        DurationSeconds = p.DurationSeconds
                    })
                    .OrderBy(p => p.JoinedAt)
                    .ToList() ?? []
            };
        }
    }
}
