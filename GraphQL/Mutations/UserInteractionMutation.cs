using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class UserInteractionMutation
    {
        public record LogInteractionInput(Guid? PostId, Guid? ReelId, int TimeSpentInSeconds, bool IsFavorite = false);
        public record UpdateFavoriteStatusInput(Guid InteractionId, bool IsFavorite);

        [Authorize]
        public async Task<ApiResponse<UserInteractionDto>> LogInteractionAsync(
            LogInteractionInput input,
            [Service] IUserInteractionService interactionService,
            [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<UserInteractionDto>.Fail("User not authenticated.");
            }

            var createDto = new CreateUserInteractionDto
            {
                UserId = currentUser.Data.Id,
                PostId = input.PostId,
                ReelId = input.ReelId,
                TimeSpentInSeconds = input.TimeSpentInSeconds,
                IsFavorite = input.IsFavorite
            };

            return await interactionService.LogOrUpdateInteractionAsync(createDto);
        }

        [Authorize]
        public async Task<ApiResponse<UserInteractionDto>> UpdateFavoriteStatusAsync(
            UpdateFavoriteStatusInput input,
            [Service] IUserInteractionService interactionService)
        {
            // Optional: Add validation to ensure the current user owns the interaction
            return await interactionService.UpdateInteractionFavoriteStatusAsync(input.InteractionId, input.IsFavorite);
        }
    }
}