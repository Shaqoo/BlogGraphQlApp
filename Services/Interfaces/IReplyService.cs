using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;


namespace BlogGraphQlApp.Services.Interfaces
{
    public interface IReplyService
    {
        Task<ApiResponse<ReplyDto>> CreateReplyAsync(CreateReplyDto createReplyDto);
        Task<ApiResponse<ReplyDto?>> GetReplyByIdAsync(Guid id);
        Task<ApiResponse<ReplyDto>> UpdateReplyAsync(Guid id, UpdateReplyDto updateReplyDto);
        Task<ApiResponse<bool>> DeleteReplyAsync(Guid id);
        Task<IQueryable<ReplyDto>> GetTopLevelRepliesAsync(Guid postId);
        Task<IQueryable<ReplyDto>> GetNestedRepliesAsync(Guid parentReplyId);
    }
}