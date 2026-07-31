using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IPostService
    {
        Task<ApiResponse<PostDto>> CreatePostAsync(CreatePostDto createPostDto);
        Task<ApiResponse<PostDto?>> GetPostByIdAsync(Guid id, Guid? currentUserId = null);
        Task<ApiResponse<IQueryable<PostDto>>> GetPostsByUserIdAsync(Guid userId);
        Task<IQueryable<Post>> GetPostsByTagAsync(string tag);
        Task<ApiResponse<IQueryable<PostDto>>> GetPostsAsync(Guid? currentUserId = null);
        Task<ApiResponse<PostDto>> UpdatePostAsync(Guid id, UpdatePostDto updatePostDto);
        Task<ApiResponse<bool>> DeletePostAsync(Guid id);
        Task<ApiResponse<object>> ViewPostAsync(Guid postId);
        Task<ApiResponse<object>> SharePostAsync(Guid postId);
    }
}