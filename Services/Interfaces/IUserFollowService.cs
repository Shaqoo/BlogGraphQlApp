using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IUserFollowService
    {
        Task<ApiResponse<IQueryable<UserDto>>> GetFollowersAsync(Guid userId);
        Task<ApiResponse<IQueryable<UserDto>>> GetFollowingAsync(Guid userId);
        Task<bool> IsUserFollowedByAsync(Guid followerId, Guid followingId);
        Task<ApiResponse<bool>> FollowUserAsync(Guid followingId);
        Task<ApiResponse<bool>> UnfollowUserAsync(Guid followingId);
    }
}