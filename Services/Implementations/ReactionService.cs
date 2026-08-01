using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class ReactionService : IReactionService
    {
        private readonly ILogger<ReactionService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;

        public ReactionService(ILogger<ReactionService> logger, IUnitOfWork unitOfWork, IMapper mapper, IAuthService authService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _authService = authService;
        }

        public async Task<ApiResponse<ReactionDto>> CreateReactionAsync(CreateReactionDto createReactionDto)
        {
            _logger.LogInformation("Attempting to create a reaction of type {ReactionType}", createReactionDto.Emoji);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
                return ApiResponse<ReactionDto>.Fail("User not authenticated.");

            var exists = await _unitOfWork.Reactions
                .Find(r =>
                    r.UserId == currentUser.Data.Id &&
                    r.MessageId == createReactionDto.MessageId &&
                    r.PostId == createReactionDto.PostId &&
                    r.ReelId == createReactionDto.ReelId &&
                    r.ReplyId == createReactionDto.ReplyId &&
                    r.GroupMessageId == createReactionDto.GroupMessageId
                )
                .FirstOrDefaultAsync();

            if (exists != null && exists.Emoji == createReactionDto.Emoji)
            {
                return ApiResponse<ReactionDto>.Fail("Reaction already exists.");
            }

            if (exists != null)
            {
                exists.Emoji = createReactionDto.Emoji;
                _unitOfWork.Reactions.Update(exists);
                await _unitOfWork.CompleteAsync();

                return ApiResponse<ReactionDto>.Success(
                    _mapper.Map<ReactionDto>(exists),
                    "Reaction updated successfully."
                );
            }

            var reaction = new Reaction
            {
                MessageId = createReactionDto.MessageId,
                ReelId = createReactionDto.ReelId,
                PostId = createReactionDto.PostId,
                ReplyId = createReactionDto.ReplyId,
                GroupMessageId = createReactionDto.GroupMessageId,
                Emoji = createReactionDto.Emoji,
                UserId = currentUser.Data.Id
            };

            if (createReactionDto.ReplyId != null)
            {
                var reply = await _unitOfWork.Replies.GetByIdAsync(createReactionDto.ReplyId.Value);
                if (reply == null)
                    return ApiResponse<ReactionDto>.Fail("Reply not found.");

                reply.ReactionCount += 1;
                _unitOfWork.Replies.Update(reply);
            }

            await _unitOfWork.Reactions.AddAsync(reaction);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<ReactionDto>.Success(
                _mapper.Map<ReactionDto>(reaction),
                "Reaction created successfully."
            );
        }


        public async Task<ApiResponse<bool>> DeleteReactionAsync(Guid id)
        {
            _logger.LogInformation("Deleting reaction with ID {ReactionId}", id);
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<bool>.Fail("User not authenticated.");
            }

            var reaction = await _unitOfWork.Reactions.GetByIdAsync(id);
            if (reaction is null)
            {
                return ApiResponse<bool>.Fail("Reaction not found.");
            }

            if (reaction.UserId != currentUser.Data.Id)
            {
                return ApiResponse<bool>.Fail("You are not authorized to delete this reaction.");
            }

            _unitOfWork.Reactions.Remove(reaction);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reaction {ReactionId} deleted successfully.", id);
            return ApiResponse<bool>.Success(true, "Reaction deleted successfully.");
        }

        public Task<IQueryable<ReactionDto>> GetReactionsByPostIdAsync(Guid postId)
        {
            _logger.LogInformation("Building IQueryable for reactions by PostId: {PostId}", postId);

            var query = _mapper.ProjectTo<ReactionDto>(
                _unitOfWork.Reactions
                    .Find(r => r.PostId == postId)
            );

            _logger.LogInformation("IQueryable for PostId {PostId} created.", postId);

            return Task.FromResult(query);
        }

        public Task<IQueryable<ReactionDto>> GetReactionsByGroupMessageIdAsync(Guid groupMessageId)
        {
            _logger.LogInformation("Building IQueryable for reactions by GroupMessageId: {GroupMessageId}", groupMessageId);

            var query = _mapper.ProjectTo<ReactionDto>(
                _unitOfWork.Reactions
                    .Find(r => r.GroupMessageId == groupMessageId)
            );

            return Task.FromResult(query);
        }

        public Task<IQueryable<ReactionDto>> GetReactionsByReplyIdAsync(Guid replyId)
        {
            _logger.LogInformation("Building IQueryable for reactions by ReplyId: {ReplyId}", replyId);

            var query = _mapper.ProjectTo<ReactionDto>(
                _unitOfWork.Reactions
                    .Find(r => r.ReplyId == replyId)
            );

            _logger.LogInformation("IQueryable for ReplyId {ReplyId} created.", replyId);

            return Task.FromResult(query);
        }

        public async Task<ApiResponse<string>> GetUserReactionToPostAsync(Guid postId)
        {
            _logger.LogInformation("Getting user reaction to post {PostId}", postId);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser?.Data == null)
            {
                _logger.LogWarning("GetUserReactionToPost called but no authenticated user found.");
                return ApiResponse<string>.Fail("User is not authenticated.");
            }

            var userId = currentUser.Data.Id;

            var reaction = await _unitOfWork.Reactions
                .Find(r => r.PostId == postId && r.UserId == userId)
                .Select(r => r.Emoji)
                .FirstOrDefaultAsync();

            if (reaction == null)
            {
                _logger.LogInformation("User {UserId} has not reacted to post {PostId}", userId, postId);
                return ApiResponse<string>.Success(string.Empty, "User has not reacted to this post.");
            }

            _logger.LogInformation("User {UserId} reacted to post {PostId} with {Reaction}", userId, postId, reaction);

            return ApiResponse<string>.Success(reaction, "Reaction retrieved successfully.");
        }


        public async Task<ApiResponse<string>> GetUserReactionToReplyAsync(Guid replyId)
        {
            _logger.LogInformation("Getting user reaction to reply {ReplyId}", replyId);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser?.Data == null)
            {
                _logger.LogWarning("GetUserReactionToReply called but no authenticated user found.");
                return ApiResponse<string>.Fail("User is not authenticated.");
            }

            var userId = currentUser.Data.Id;

            var reaction = await _unitOfWork.Reactions
                .Find(r => r.ReplyId == replyId && r.UserId == userId)
                .Select(r => r.Emoji)
                .FirstOrDefaultAsync();

            if (reaction == null)
            {
                _logger.LogInformation("User {UserId} has not reacted to reply {ReplyId}", userId, replyId);
                return ApiResponse<string>.Success(string.Empty, "User has not reacted to this reply.");
            }

            _logger.LogInformation("User {UserId} reacted to reply {ReplyId} with {Reaction}", userId, replyId, reaction);

            return ApiResponse<string>.Success(reaction, "Reaction retrieved successfully.");
        }


        public async Task<ApiResponse<bool>> HasUserReactedToPostAsync(Guid postId)
        {
            _logger.LogInformation("Checking if current user reacted to post {PostId}", postId);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.Data == null)
            {
                _logger.LogWarning("HasReacted called but no authenticated user found.");
                return ApiResponse<bool>.Fail("User is not authenticated.");
            }

            var userId = currentUser.Data.Id;

            var hasReacted = await _unitOfWork.Reactions
                .AnyAsync(a => a.PostId == postId && a.UserId == userId);

            _logger.LogInformation("User {UserId} has reacted to post {PostId}: {HasReacted}",
                userId, postId, hasReacted);

            return ApiResponse<bool>.Success(hasReacted);
        }

        public async Task<ApiResponse<bool>> HasUserReactedToReplyAsync(Guid replyId)
        {
            _logger.LogInformation("Checking if current user reacted to reply {ReplyId}", replyId);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.Data == null)
            {
                _logger.LogWarning("HasReacted called but no authenticated user found.");
                return ApiResponse<bool>.Fail("User is not authenticated.");
            }

            var userId = currentUser.Data.Id;

            var hasReacted = await _unitOfWork.Reactions
                .AnyAsync(a => a.ReplyId == replyId && a.UserId == userId);

            _logger.LogInformation("User {UserId} has reacted to reply {ReplyId}: {HasReacted}",
                userId, replyId, hasReacted);

            return ApiResponse<bool>.Success(hasReacted);
        }
    }
}