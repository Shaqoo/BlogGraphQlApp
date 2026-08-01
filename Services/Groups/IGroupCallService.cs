using BlogGraphQlApp.Common;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupCallService
    {
        Task<ApiResponse<GroupCallDto>> StartAsync(Guid groupId, Guid startedById, CallMediaType mediaType, CancellationToken ct = default);
        Task<ApiResponse<GroupCallDto>> JoinAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> LeaveAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> EndAsync(Guid callId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleMuteAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleCameraAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleScreenshareAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleHandRaisedAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<GroupCallDto>> GetAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<GroupCallDto>> GetTokenAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupCallParticipantDto>>> GetParticipantsAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupCallDto>>> GetActiveCallsAsync(Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<CallHistoryDto>>> GetHistoryAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task MarkEndedAsync(Guid callId, CancellationToken ct = default);
    }
}
