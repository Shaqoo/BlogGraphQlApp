using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Implementations
{
    public class UserRecommendationService : IUserRecommendationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UserRecommendationService(IUnitOfWork unitOfWork,IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public Task<IQueryable<UserDto>> GetRecommendedUsers(Guid currentUserId)
        {
            var users = _unitOfWork.Users.GetAll();
            var follows = _unitOfWork.UserFollows.GetAll();

            // IDs of users current user follows
            var followingIds =
                follows
                    .Where(f => f.FollowerId == currentUserId)
                    .Select(f => f.FollowingId);

            // Friends of friends
            var friendsOfFriendsIds =
                follows
                    .Where(f => followingIds.Contains(f.FollowerId))
                    .Select(f => f.FollowingId);

            // Mutual followers
            var mutualFollowersIds =
                follows
                    .Where(f => f.FollowingId == currentUserId)
                    .SelectMany(f =>
                        follows.Where(x => x.FollowerId == f.FollowerId))
                    .Select(x => x.FollowingId);

            var candidateIds =
                friendsOfFriendsIds
                    .Union(mutualFollowersIds)
                    .Distinct();

            // Detect empty candidate set (user has no followers or following)
            var hasCandidates = candidateIds.Any();

            IQueryable<User> query;

            if (hasCandidates)
            {
                // Normal recommendation
                query =
                    users
                        .Where(u => u.Id != currentUserId)
                        .Where(u => !followingIds.Contains(u.Id))
                        .Where(u => candidateIds.Contains(u.Id));
            }
            else
            {
                // Fallback: top 50 users or random users or newly active users
                query =
                    users
                        .Where(u => u.Id != currentUserId)
                        .OrderByDescending(u => u.CreatedAt);  // or activity/popularity
            }

            return Task.FromResult(_mapper.ProjectTo<UserDto>(query));
        }

    }

}
