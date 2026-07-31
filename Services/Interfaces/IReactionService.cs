﻿using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IReactionService
    {
        Task<ApiResponse<ReactionDto>> CreateReactionAsync(CreateReactionDto createReactionDto);
        Task<ApiResponse<bool>> DeleteReactionAsync(Guid id);
        Task<ApiResponse<bool>> HasUserReactedToPostAsync(Guid postId);
        Task<ApiResponse<bool>> HasUserReactedToReplyAsync(Guid replyId);
        Task<ApiResponse<string>> GetUserReactionToReplyAsync(Guid replyId);
        Task<ApiResponse<string>> GetUserReactionToPostAsync(Guid postId);
        Task<IQueryable<ReactionDto>> GetReactionsByReplyIdAsync(Guid replyId);
        Task<IQueryable<ReactionDto>> GetReactionsByPostIdAsync(Guid postId);
    }
}