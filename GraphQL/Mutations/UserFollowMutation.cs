using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class UserFollowMutation
    {
        [Authorize]
        [GraphQLDescription("Follows a user specified by their ID.")]
        public async Task<ApiResponse<bool>> FollowUserAsync(Guid userId, [Service] IUserFollowService followService)
        {
            return await followService.FollowUserAsync(userId);
        }

        [Authorize]
        [GraphQLDescription("Unfollows a user specified by their ID.")]
        public async Task<ApiResponse<bool>> UnfollowUserAsync(Guid userId, [Service] IUserFollowService followService)
        {
            return await followService.UnfollowUserAsync(userId);
        }
    }
}