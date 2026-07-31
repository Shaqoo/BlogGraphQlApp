using BlogGraphQlApp.Common;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IMessagingService
    {
        Task<ApiResponse<MessageDto>> SendMessageAsync(Guid toUserId,MessageType messageType, string? content, IFile? file,Guid? replyToMessageId);
        Task<ApiResponse<MessageDto?>> GetMessageByIdAsync(Guid messageId);
        Task<ApiResponse<bool>> MarkAsReadAsync(Guid messageId);
        Task<ApiResponse<bool>> DeleteMessageAsync(Guid messageId);
        Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid conversationId);
        Task<ApiResponse<IQueryable<ConversationDto>>> GetConversationsAsync();
        Task<ApiResponse<IQueryable<MessageDto>>> GetMessagesAsync(Guid conversationId);
        Task<ApiResponse<AgoraTokenDto>> GenerateVideoCallTokensAsync(Guid toUserId);
        Task<bool> CanMessageUserAsync(Guid fromUserId, Guid toUserId);
    }
}