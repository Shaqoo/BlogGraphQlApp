using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using BlogGraphQlApp.Models;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class PostQueries
    {
        private readonly ILogger<PostQueries> _logger;
        public PostQueries(ILogger<PostQueries> logger)
        {
            _logger = logger;
        }
        [Authorize]
        public async Task<ApiResponse<PostDto?>> GetPostByIdAsync(Guid id, [Service] IPostService postService)
        {
            return await postService.GetPostByIdAsync(id);
        }

        [Authorize]
        public async Task<ApiResponse<IQueryable<PostDto>>> GetPostsByUserIdAsync(Guid userId, [Service] IPostService postService)
        => await postService.GetPostsByUserIdAsync(userId);

        [Authorize]
        [UsePaging(typeof(PostType))]
        [GraphQLDescription("Gets a paginated feed of posts from users the current user follows.")]
        public async Task<IQueryable<PostDto>> GetPostFeedAsync(
            [Service] IPostService postService)
        {
            var response = await postService.GetPostsAsync();
            return response.Data!; 
        }


        [Authorize]
        [UsePaging(typeof(PostType))]
        [GraphQLDescription("Gets a paginated list of posts by hashtag tag.")]
        public async Task<IQueryable<Post>> GetPostsByTagAsync(
        string tag,
        [Service] IPostService postService)
        {
            return await postService.GetPostsByTagAsync(tag);
        }

        public async Task<PaginatedResult<PostDto>> GetRecommendedPostsAsync(
       int page,
       int pageSize,
       [Service] IAuthService authService,
       [Service] IRecommendationService recommendationService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                _logger.LogWarning("Unauthorized attempt to fetch recommended posts.");
                return PaginatedResult<PostDto>.Create([], page, pageSize,0);
            }
            _logger.LogInformation("Fetching recommended posts for user {UserId}, page {Page}, pageSize {PageSize}", currentUser.Data.Id, page, pageSize);
            return await recommendationService.GetRecommendedPostsAsync(currentUser.Data.Id, page, pageSize);
        }

    }
}