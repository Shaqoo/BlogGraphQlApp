using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Services.Video
{
    public interface IVideoCallService
    {
        Task<ApiResponse<VideoCallDto>> StartAsync(Guid callerId, Guid recipientId, CancellationToken cancellationToken = default);
        Task<ApiResponse<VideoCallDto>> AcceptAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> RejectAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> EndAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<VideoCallDto>> GetAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<VideoCallDto>> GetTokenAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
    }
}
