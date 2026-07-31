using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Mscc.GenerativeAI;

namespace BlogGraphQlApp.Services.Implementations
{
    public class ReplyService : IReplyService
    {
        private readonly ILogger<ReplyService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;

        public ReplyService(ILogger<ReplyService> logger, IUnitOfWork unitOfWork, IMapper mapper, IAuthService authService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _authService = authService;
        }

        public async Task<ApiResponse<ReplyDto>> CreateReplyAsync(CreateReplyDto createReplyDto)
        {
            _logger.LogInformation("Attempting to create a reply.");
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<ReplyDto>.Fail("User not authenticated.");
            }
            
            var parentReply = createReplyDto.ParentReplyId.HasValue
                ? await _unitOfWork.Replies.GetByIdAsync(createReplyDto.ParentReplyId.Value)
                : null;


            var reply = _mapper.Map<Reply>(createReplyDto);
            reply.UserId = currentUser.Data.Id;

            await _unitOfWork.Replies.AddAsync(reply);
            if (parentReply != null)
            {
                parentReply.NestedReplyCount += 1;
                _unitOfWork.Replies.Update(parentReply);
            }

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reply {ReplyId} created successfully by user {UserId}", reply.Id, currentUser.Data.Id);

            var replyDto = _mapper.Map<ReplyDto>(reply);
            return ApiResponse<ReplyDto>.Success(replyDto, "Reply created successfully.");
        }

        public async Task<ApiResponse<bool>> DeleteReplyAsync(Guid id)
        {
            _logger.LogInformation("Deleting reply with ID {ReplyId}", id);
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<bool>.Fail("User not authenticated.");
            }

            var reply = await _unitOfWork.Replies.GetByIdAsync(id);
            if (reply is null)
            {
                return ApiResponse<bool>.Fail("Reply not found.");
            }

            if (reply.UserId != currentUser.Data.Id)
            {
                return ApiResponse<bool>.Fail("You are not authorized to delete this reply.");
            }

            _unitOfWork.Replies.Remove(reply);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reply {ReplyId} deleted successfully.", id);
            return ApiResponse<bool>.Success(true, "Reply deleted successfully.");
        }

        public Task<IQueryable<ReplyDto>> GetNestedRepliesAsync(Guid parentReplyId)
        {
            var replies = _unitOfWork.Replies.Find(r => r.ParentReplyId == parentReplyId)
                           .OrderBy(r => r.CreatedAt)
                           .Include(r => r.User);

            var repliesDto = _mapper.ProjectTo<ReplyDto>(replies);

            return Task.FromResult(repliesDto);
        }

        public async Task<ApiResponse<ReplyDto?>> GetReplyByIdAsync(Guid id)
        {
            _logger.LogInformation("Getting reply with ID {ReplyId}", id);
            var reply = await _unitOfWork.Replies
                .Find(r => r.Id == id)
                .Include(r => r.User)
                .FirstOrDefaultAsync();

            if (reply is null)
            {
                return ApiResponse<ReplyDto?>.Fail($"Reply with ID {id} not found.");
            }

            var replyDto = _mapper.Map<ReplyDto>(reply);
            replyDto.NestedReplyCount = await _unitOfWork.Replies.CountAsync(r => r.ParentReplyId == id);

            return ApiResponse<ReplyDto?>.Success(replyDto);
        }

        public Task<IQueryable<ReplyDto>> GetTopLevelRepliesAsync(Guid postId)
        {
            var replies = _unitOfWork.Replies.Find(r => r.PostId == postId && r.ParentReplyId == null)
                    .OrderBy(r => r.CreatedAt)
                    .Include(r => r.User);

            var repliesDto = _mapper.ProjectTo<ReplyDto>(replies);

            return Task.FromResult(repliesDto);
        }

        public async Task<ApiResponse<ReplyDto>> UpdateReplyAsync(Guid id, UpdateReplyDto updateReplyDto)
        {
            _logger.LogInformation("Updating reply with ID {ReplyId}", id);
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                return ApiResponse<ReplyDto>.Fail("User not authenticated.");
            }

            var reply = await _unitOfWork.Replies.GetByIdAsync(id);
            if (reply is null)
            {
                return ApiResponse<ReplyDto>.Fail("Reply not found.");
            }

            if (reply.UserId != currentUser.Data.Id)
            {
                return ApiResponse<ReplyDto>.Fail("You are not authorized to update this reply.");
            }

            _mapper.Map(updateReplyDto, reply);
            _unitOfWork.Replies.Update(reply);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Reply {ReplyId} updated successfully.", id);
            var replyDto = _mapper.Map<ReplyDto>(reply);

            return ApiResponse<ReplyDto>.Success(replyDto, "Reply updated successfully.");
        }
    }
}