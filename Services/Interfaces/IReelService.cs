﻿﻿﻿using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IReelService
    {
        Task<ApiResponse<ReelDto>> CreateReelAsync(CreateReelDto createReelDto);
        Task<ApiResponse<ReelDto?>> GetReelByIdAsync(Guid id);
        Task<ApiResponse<IQueryable<ReelDto>>> GetReelFeedAsync(Guid? currentUserId = null);
        Task<ApiResponse<IEnumerable<ReelDto>>> GetReelsByUserIdAsync(Guid userId);
        Task<ApiResponse<ReelDto>> UpdateReelAsync(Guid id, UpdateReelDto updateReelDto);
        Task<ApiResponse<bool>> DeleteReelAsync(Guid id);
    }
}