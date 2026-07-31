using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IUserInteractionService
    {
        Task<ApiResponse<UserInteractionDto>> LogOrUpdateInteractionAsync(CreateUserInteractionDto createDto);
        Task<ApiResponse<UserInteractionDto>> UpdateInteractionFavoriteStatusAsync(Guid interactionId, bool isFavorite);
    }
}