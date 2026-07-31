using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Services.Interfaces
{
    public interface IUserRecommendationService
    {
        Task<IQueryable<UserDto>> GetRecommendedUsers(Guid currentUserId);
    }
}
