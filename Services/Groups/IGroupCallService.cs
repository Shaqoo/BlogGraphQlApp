using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupCallService
    {
        Task<ApiResponse<GroupCallDto>> StartAsync(Guid groupId, Guid startedById, CancellationToken cancellationToken = default);
        Task<ApiResponse<GroupCallDto>> JoinAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> EndAsync(Guid callId, Guid actorId, CancellationToken cancellationToken = default);
        Task<ApiResponse<GroupCallDto>> GetAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<GroupCallDto>> GetTokenAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task MarkEndedAsync(Guid callId, CancellationToken cancellationToken = default);
    }
}
