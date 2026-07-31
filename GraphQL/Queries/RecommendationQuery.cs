using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using BlogGraphQlApp.Services.Interfaces;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class RecommendationQuery
    {
        [Authorize]
        [GraphQLDescription("Gets personalized post recommendations for the current user.")]
        public async Task<ApiResponse<IEnumerable<PostDto>>> GetPostRecommendations(
            [Service] IRecommendationService recommendationService,
            int limit = 10)
        {
            return await recommendationService.GetPostRecommendationsAsync(limit);
        }

        [Authorize]
        [GraphQLDescription("Gets personalized reel recommendations for the current user.")]
        public async Task<ApiResponse<IEnumerable<ReelDto>>> GetReelRecommendations(
            [Service] IRecommendationService recommendationService,
            int limit = 10)
        {
            return await recommendationService.GetReelRecommendationsAsync(limit);
        }

        [Authorize]
        [UsePaging(typeof(UserType))]
        [GraphQLDescription("Gets personalized user recommendations for the current user.")]
        public async Task<IQueryable<UserDto>> GetUserRecommendations(
    [Service] IUserRecommendationService userRecommendationService,
    [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();

            if (currentUser.Data is null)
            {
                throw new GraphQLException("Current user not found.");
            }

            var recommendedUsers = await userRecommendationService.GetRecommendedUsers(currentUser.Data.Id);

            return recommendedUsers;
        }

    }
}