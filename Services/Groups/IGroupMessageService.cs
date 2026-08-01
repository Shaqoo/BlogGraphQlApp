using BlogGraphQlApp.Common;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Storage;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupMessageService
    {
        Task<ApiResponse<GroupMessageDto>> SendAsync(Guid groupId, Guid senderId, MessageType messageType, string? content, IFile? file, Guid? replyToMessageId, CancellationToken ct = default);
        Task<ApiResponse<GroupMessageDto>> EditAsync(Guid groupId, Guid messageId, Guid senderId, string content, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeleteAsync(Guid groupId, Guid messageId, Guid senderId, CancellationToken ct = default);
        Task<ApiResponse<GroupMessageDto>> SetPinnedAsync(Guid groupId, Guid messageId, Guid actorId, bool pin, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleReactionAsync(Guid groupId, Guid messageId, Guid userId, string emoji, CancellationToken ct = default);
        Task<ApiResponse<bool>> RemoveReactionAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MarkDeliveredAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MarkReadAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MarkAllReadAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<GroupMessageDto>> GetMessageAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetPinnedMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> SearchAsync(Guid groupId, Guid userId, GroupMessageSearchInput input, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMediaAsync(Guid groupId, Guid userId, MessageType? mediaType, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<int>> GetUnreadCountAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<int>> GetUnreadGroupCountAsync(Guid userId, CancellationToken ct = default);
        Task<Dictionary<Guid, int>> GetUnreadCountsByGroupAsync(Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMyMentionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task<GroupMessage> InsertSystemMessageAsync(ChatGroup group, Guid actorId, string content, string? metadata = null, CancellationToken ct = default);
    }
}
