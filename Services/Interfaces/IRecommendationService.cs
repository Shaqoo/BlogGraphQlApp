using BlogGraphQlApp.Common;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IRecommendationService
    {
        Task<ApiResponse<IEnumerable<PostDto>>> GetPostRecommendationsAsync(int limit = 10);
        Task<ApiResponse<IEnumerable<ReelDto>>> GetReelRecommendationsAsync(int limit = 10);

        Task<PaginatedResult<PostDto>> GetRecommendedPostsAsync(
          Guid userId,
          int page = 1,
          int pageSize = 10);
    }
    
}