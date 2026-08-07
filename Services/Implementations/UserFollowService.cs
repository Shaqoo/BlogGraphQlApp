using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Push;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class UserFollowService : IUserFollowService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly IWebPushService _webPush;
        private readonly ILogger<UserFollowService> _logger;

        public UserFollowService(IUnitOfWork unitOfWork, IAuthService authService, IWebPushService webPush, ILogger<UserFollowService> logger)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _webPush = webPush;
            _logger = logger;
        }

        public async Task<ApiResponse<bool>> FollowUserAsync(Guid followingId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<bool>.Fail("User not authenticated.");

            var followerId = currentUserResponse.Data.Id;

            if (followerId == followingId)
                return ApiResponse<bool>.Fail("You cannot follow yourself.");

            var targetUserExists = await _unitOfWork.Users
                .Find(u => u.Id == followingId)
                .AnyAsync();

            if (!targetUserExists)
                return ApiResponse<bool>.Fail("User does not exist.");

            var alreadyFollowing = await _unitOfWork.UserFollows
                .Find(f => f.FollowerId == followerId && f.FollowingId == followingId)
                .AnyAsync();

            if (alreadyFollowing)
                return ApiResponse<bool>.Fail("You are already following this user.");

            await _unitOfWork.UserFollows.AddAsync(new UserFollow
            {
                FollowerId = followerId,
                FollowingId = followingId
            });

            var conversationExists = await _unitOfWork.Conversations
                .Find(c => c.Participants.Any(p => p.Id == followerId) &&
                           c.Participants.Any(p => p.Id == followingId))
                .AnyAsync();

            if (!conversationExists)
            {
                var participants = await _unitOfWork.Users
                    .Find(u => u.Id == followerId || u.Id == followingId)
                    .ToListAsync();

                await _unitOfWork.Conversations.AddAsync(new Conversation
                {
                    Participants = participants
                });
            }

            await NotifyUserAsync($"{currentUserResponse.Data.Username} started following you.", followingId, NotificationType.NewFollower);

            await _unitOfWork.CompleteAsync();

            await SendFollowPushAsync(currentUserResponse.Data, followingId);

            _logger.LogInformation("User {FollowerId} started following user {FollowingId}", followerId, followingId);
            return ApiResponse<bool>.Success(true, "Successfully followed user.");
        }

        private async Task SendFollowPushAsync(UserDto follower, Guid followingId)
        {
            var payload = new FollowPushPayload
            {
                FollowerId = follower.Id,
                FollowerName = follower.FullName,
                FollowerAvatar = follower.ProfilePictureUrl,
                Url = $"/profile/{follower.Id}"
            };

            await _webPush.SendToUserAsync(followingId, payload);
        }


        public Task<ApiResponse<IQueryable<UserDto>>> GetFollowersAsync(Guid userId)
        {
            _logger.LogInformation("Getting followers for user {UserId}", userId);

            var followersQuery = _unitOfWork.UserFollows
                .Find(f => f.FollowingId == userId)
                .Include(f => f.Follower)
                .OrderBy(f => f.Follower.Username) // Order for stable pagination
                .Select(f => new UserDto
                {
                    Id = f.Follower.Id,
                    Username = f.Follower.Username,
                    FullName = f.Follower.FullName,
                    ProfilePictureUrl = f.Follower.ProfilePictureUrl,
                    BackgroundIdentifier = f.Follower.BackgroundIdentifier,
                    Bio = f.Follower.Bio,
                    CoverPictureUrl = f.Follower.CoverPictureUrl,
                    CreatedAt = f.Follower.CreatedAt,
                    Email = f.Follower.Email,
                    PhoneNumber = f.Follower.PhoneNumber,
                    ReelsCount = f.Follower.Reels.Count,
                    PostsCount = f.Follower.Posts.Count,
                    LastSeen = f.Follower.LastSeen
                    // Map other necessary fields
                });

            return Task.FromResult(ApiResponse<IQueryable<UserDto>>.Success(followersQuery));
        }

        public Task<ApiResponse<IQueryable<UserDto>>> GetFollowingAsync(Guid userId)
        {
            _logger.LogInformation("Getting users followed by user {UserId}", userId);

            var followingQuery = _unitOfWork.UserFollows
                .Find(f => f.FollowerId == userId)
                .Include(f => f.Following)
                .OrderBy(f => f.Following.Username) // Order for stable pagination
                .Select(f => new UserDto
                {
                    Id = f.Following.Id,
                    Username = f.Following.Username,
                    FullName = f.Following.FullName,
                    ProfilePictureUrl = f.Following.ProfilePictureUrl,
                    BackgroundIdentifier = f.Follower.BackgroundIdentifier,
                    Bio = f.Follower.Bio,
                    CoverPictureUrl = f.Follower.CoverPictureUrl,
                    CreatedAt = f.Follower.CreatedAt,
                    Email = f.Follower.Email,
                    PhoneNumber = f.Follower.PhoneNumber,
                    ReelsCount = f.Follower.Reels.Count,
                    PostsCount = f.Follower.Posts.Count,
                    LastSeen = f.Follower.LastSeen
                    // Map other necessary fields
                });

            return Task.FromResult(ApiResponse<IQueryable<UserDto>>.Success(followingQuery));
        }

        public async Task<bool> IsUserFollowedByAsync(Guid followerId, Guid followingId)
        {
            var userFollowExists = await _unitOfWork.UserFollows
                .Find(f => f.FollowerId == followerId && f.FollowingId == followingId)
                .AnyAsync();
           
            _logger.LogInformation("Checking if user {FollowerId} follows user {FollowingId}: {IsFollowing}", followerId, followingId, userFollowExists);
            return userFollowExists;
        }

        public async Task<ApiResponse<bool>> UnfollowUserAsync(Guid followingId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<bool>.Fail("User not authenticated.");

            var followerId = currentUserResponse.Data.Id;

            var follow = await _unitOfWork.UserFollows
                .Find(f => f.FollowerId == followerId && f.FollowingId == followingId)
                .FirstOrDefaultAsync();

            if (follow == null)
                return ApiResponse<bool>.Fail("You are not following this user.");

            _unitOfWork.UserFollows.Remove(follow);

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("User {FollowerId} unfollowed user {FollowingId}", followerId, followingId);

            return ApiResponse<bool>.Success(true, "Successfully unfollowed user.");
        }

        private async Task NotifyUserAsync(string message, Guid userId, NotificationType notificationType)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Message = message,
                NotificationType = notificationType,
                UserId = userId
            });
        }
    }
}