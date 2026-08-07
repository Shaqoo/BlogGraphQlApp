using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using BlogGraphQlApp.Models;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class PostQueries
    {
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

    }
}