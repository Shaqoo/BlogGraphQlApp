﻿using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class ReelService : IReelService
    {
        private readonly ILogger<ReelService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;
        private readonly IUploadService _uploadService;

        public ReelService(ILogger<ReelService> logger, IUnitOfWork unitOfWork, IMapper mapper, IAuthService authService, IUploadService uploadService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _authService = authService;
            _uploadService = uploadService;
        }

        public async Task<ApiResponse<ReelDto>> CreateReelAsync(CreateReelDto createReelDto)
        {
            _logger.LogInformation("Attempting to create a reel.");
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<ReelDto>.Fail("User not authenticated.");
            }

            var videoPath = await _uploadService.UploadFileAsync(createReelDto.Video, "reels");
            if (videoPath is null)
            {
                return ApiResponse<ReelDto>.Fail("Video upload failed.");
            }

            var reel = _mapper.Map<Reel>(createReelDto);
            reel.UserId = currentUser.Data.Id;
            reel.VideoUrl = videoPath;

            await _unitOfWork.Reels.AddAsync(reel);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reel {ReelId} created successfully by user {UserId}", reel.Id, currentUser.Data.Id);
            var reelDto = _mapper.Map<ReelDto>(reel);
            return ApiResponse<ReelDto>.Success(reelDto, "Reel created successfully.");
        }

        public async Task<ApiResponse<bool>> DeleteReelAsync(Guid id)
        {
            _logger.LogInformation("Deleting reel with ID {ReelId}", id);
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<bool>.Fail("User not authenticated.");
            }

            var reel = await _unitOfWork.Reels.GetByIdAsync(id);
            if (reel is null)
            {
                return ApiResponse<bool>.Fail("Reel not found.");
            }

            if (reel.UserId != currentUser.Data.Id)
            {
                return ApiResponse<bool>.Fail("You are not authorized to delete this reel.");
            }

            await _uploadService.DeleteFileAsync(reel.VideoUrl);
            _unitOfWork.Reels.Remove(reel);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reel {ReelId} deleted successfully.", id);
            return ApiResponse<bool>.Success(true, "Reel deleted successfully.");
        }

        public async Task<ApiResponse<ReelDto?>> GetReelByIdAsync(Guid id)
        {
            _logger.LogInformation("Getting reel with ID {ReelId}", id);
            var reel = await _unitOfWork.Reels.Find(r => r.Id == id).Include(r => r.User).FirstOrDefaultAsync();
            if (reel is null)
            {
                return ApiResponse<ReelDto?>.Fail($"Reel with ID {id} not found.");
            }

            var reelDto = _mapper.Map<ReelDto>(reel);
            reelDto.ReplyCount = await _unitOfWork.Replies.CountAsync(r => r.ReelId == id);
            reelDto.ReactionCount = await _unitOfWork.Reactions.CountAsync(r => r.ReelId == id);

            return ApiResponse<ReelDto?>.Success(reelDto);
        }

        public async Task<ApiResponse<IEnumerable<ReelDto>>> GetReelsByUserIdAsync(Guid userId)
        {
            _logger.LogInformation("Getting reels for user ID {UserId}", userId);
            var reels = await _unitOfWork.Reels.Find(r => r.UserId == userId).Include(r => r.User).ToListAsync();
            var reelDtos = _mapper.Map<IEnumerable<ReelDto>>(reels);
            return ApiResponse<IEnumerable<ReelDto>>.Success(reelDtos);
        }

        public async Task<ApiResponse<IQueryable<ReelDto>>> GetReelFeedAsync(Guid? currentUserId = null)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
            {
                _logger.LogWarning("Attempt to get reel feed without a logged-in user.");
                return ApiResponse<IQueryable<ReelDto>>.Fail("User not authenticated.");
            }

            var userId = currentUserId ?? currentUserResponse.Data.Id;
            _logger.LogInformation("Getting paginated reel feed for user {UserId}", userId);

            var followingIds = await _unitOfWork.UserFollows
                .Find(f => f.FollowerId == userId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            // Include the user's own reels in their feed
            followingIds.Add(userId);

            var reelsQuery = _unitOfWork.Reels
                .Find(r => followingIds.Contains(r.UserId))
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id) // Secondary sort for stable ordering
                .Select(r => new ReelDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    VideoUrl = r.VideoUrl,
                    CreatedAt = r.CreatedAt,
                    User = _mapper.Map<UserDto>(r.User),
                    ReactionCount = r.Reactions.Count(),
                    ReplyCount = r.Replies.Count(),
                    Views = r.Views
                });

            return ApiResponse<IQueryable<ReelDto>>.Success(reelsQuery);
        }

        public async Task<ApiResponse<ReelDto>> UpdateReelAsync(Guid id, UpdateReelDto updateReelDto)
        {
            _logger.LogInformation("Updating reel with ID {ReelId}", id);
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<ReelDto>.Fail("User not authenticated.");
            }

            var reel = await _unitOfWork.Reels.GetByIdAsync(id);
            if (reel is null)
            {
                return ApiResponse<ReelDto>.Fail("Reel not found.");
            }

            if (reel.UserId != currentUser.Data.Id)
            {
                return ApiResponse<ReelDto>.Fail("You are not authorized to update this reel.");
            }

            _mapper.Map(updateReelDto, reel);
            _unitOfWork.Reels.Update(reel);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reel {ReelId} updated successfully.", id);
            return ApiResponse<ReelDto>.Success(_mapper.Map<ReelDto>(reel), "Reel updated successfully.");
        }
    }
}