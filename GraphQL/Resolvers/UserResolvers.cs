using AutoMapper;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public sealed class UserResolvers
    {
#pragma warning disable CC0091 
        public async Task<IEnumerable<NotificationDto>> GetNotificationsAsync(
#pragma warning restore CC0091  
            [Parent] UserDto user,
            NotificationByUserIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var notifications = await dataLoader.LoadAsync(user.Id, cancellationToken);
            return mapper.Map<IEnumerable<NotificationDto>>(notifications);
        }

        public async Task<IEnumerable<UserDto>> GetFollowersAsync(
            [Parent] UserDto user,
            [Service] IUnitOfWork unitOfWork)
        {
            var followerIds = await unitOfWork.UserFollows
                .Find(f => f.FollowingId == user.Id)
                .Select(f => f.FollowerId)
                .ToListAsync();

            return await unitOfWork.Users.Find(u => followerIds.Contains(u.Id)).Select(u => new UserDto { Id = u.Id, Username = u.Username, FullName = u.FullName, ProfilePictureUrl = u.ProfilePictureUrl }).ToListAsync();
        }

        public async Task<IEnumerable<UserDto>> GetFollowingAsync(
            [Parent] UserDto user,
            [Service] IUnitOfWork unitOfWork)
        {
            var followingIds = await unitOfWork.UserFollows
                .Find(f => f.FollowerId == user.Id)
                .Select(f => f.FollowingId)
                .ToListAsync();

            return await unitOfWork.Users.Find(u => followingIds.Contains(u.Id)).Select(u => new UserDto { Id = u.Id, Username = u.Username, FullName = u.FullName, ProfilePictureUrl = u.ProfilePictureUrl }).ToListAsync();
        }

        public async Task<bool> GetIsFollowedByCurrentUser(
            [Parent] UserDto userDto,
            [Service] IUserFollowService userFollowService,
            [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            if (currentUser.Data == null)
            {
                return false;
            }
            return await userFollowService.IsUserFollowedByAsync(currentUser.Data.Id, userDto.Id);
        }

        public async Task<bool> CheckIfIsOnline(
            [Parent] UserDto userDto,
            [Service] PresenceTracker presenceTracker)
        {
            return await presenceTracker.IsOnline(userDto.Id);
        }
    }
}