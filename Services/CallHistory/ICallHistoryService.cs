using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Services.History
{
    public interface ICallHistoryService
    {
        Task StartDirectAsync(Guid callId, Guid callerId, Guid recipientId, string roomName, DateTime startedAt, CancellationToken ct = default);
        Task StartGroupAsync(Guid callId, Guid callerId, Guid groupId, string roomName, DateTime startedAt, CancellationToken ct = default);
        Task MarkAnsweredAsync(Guid callId, DateTime answeredAt, CancellationToken ct = default);
        Task EndDirectAsync(Guid callId, DateTime endedAt, Guid? endedByUserId, CancellationToken ct = default);
        Task RejectDirectAsync(Guid callId, DateTime endedAt, Guid? endedByUserId, CancellationToken ct = default);
        Task MissDirectAsync(Guid callId, DateTime endedAt, CancellationToken ct = default);
        Task AddGroupParticipantAsync(Guid callId, Guid userId, DateTime? joinedAt, CancellationToken ct = default);
        Task EndGroupAsync(Guid callId, DateTime endedAt, CancellationToken ct = default);

        Task<PaginatedResult<CallHistoryDto>> GetHistoryAsync(Guid userId, CallHistoryQuery query, CancellationToken ct = default);
        Task<CallHistoryDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default);
        Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
        Task<int> DeleteAllAsync(Guid userId, CancellationToken ct = default);
    }
}
