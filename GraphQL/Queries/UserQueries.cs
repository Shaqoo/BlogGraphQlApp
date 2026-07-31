using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class UserQueries
    {
        [Authorize]
        public async Task<ApiResponse<UserDto?>> GetMeAsync([Service] IAuthService authService)
        {
            return await authService.GetCurrentUserAsync();
        }

        public async Task<ApiResponse<UserDto?>> GetUserByIdAsync(Guid id, [Service] IUserService userService)
        {
            return await userService.GetUserByIdAsync(id);
        }

        public async Task<ApiResponse<UserDto?>> GetUserByUsernameAsync(string username, [Service] IUserService userService)
        {
            return await userService.GetUserByUsernameAsync(username);
        }

        public async Task<ApiResponse<IEnumerable<UserDto>>> GetUsersAsync([Service] IUserService userService)
        {
            return await userService.GetAllUsersAsync();
        }

        [UsePaging(typeof(UserType))]
        [GraphQLDescription("Gets the list of followers for a specific user.")]
        public async Task<IQueryable<UserDto>> GetFollowersByUsername(
            string username,
            [Service] IUserService userService,
            [Service] IUserFollowService userFollowService)
        {
            var user = await userService.GetUserByUsernameAsync(username);
            if (user == null || user.Data is null) throw new GraphQLException("Username not found");
            var response = await userFollowService.GetFollowersAsync(user.Data.Id);
            return response.Data!;
        }

        [UsePaging(typeof(UserType))]
        [GraphQLDescription("Gets the list of users a specific user is following.")]
        public async Task<IQueryable<UserDto>> GetFollowingByUsername(
            string username,
            [Service] IUserService userService,
            [Service] IUserFollowService userFollowService)
        {
            var user = await userService.GetUserByUsernameAsync(username);
            if (user == null || user.Data is null) throw new GraphQLException("Username not found");
            var response = await userFollowService.GetFollowingAsync(user.Data.Id);
            return response.Data!;
        }

        [UsePaging(typeof(UserType))]
        [GraphQLDescription("Searches for users by username, email, or full name using full-text search.")]
        public async Task<IQueryable<UserDto>> SearchUsers(
            string searchTerm,
            [Service] IUserService userService)
        {
            var response = await userService.SearchUsersAsync(searchTerm);
            return response.Data!;
        }

        public async Task<ApiResponse<bool>> CheckIfEmailExists(string email, [Service] IUserService userService)
       => await userService.CheckIfEmailExists(email);

        public async Task<ApiResponse<bool>> CheckIfUsernameExists(string username, [Service] IUserService userService)
            => await userService.CheckIfUsernameExists(username);
    }
}