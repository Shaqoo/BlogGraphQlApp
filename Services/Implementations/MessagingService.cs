using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using BlogGraphQlApp.Storage;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class MessagingService : IMessagingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly IAgoraService _agoraService;
        private readonly IFileStorage _fileStorage;
        private readonly IMapper _mapper;
        private readonly ILogger<MessagingService> _logger;

        public MessagingService(IUnitOfWork unitOfWork, IAuthService authService, IAgoraService agoraService, IFileStorage fileStorage, IMapper mapper, ILogger<MessagingService> logger)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _agoraService = agoraService;
            _fileStorage = fileStorage;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<MessageDto>> SendMessageAsync(Guid toUserId,MessageType messageType ,string? content, IFile? file, Guid? replyToMessageId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<MessageDto>.Fail("User not authenticated.");

            var fromUserId = currentUserResponse.Data.Id;

            if (!await CanMessageUserAsync(fromUserId, toUserId) && replyToMessageId is null)
                return ApiResponse<MessageDto>.Fail("You can only message users you are following.");

            if (string.IsNullOrWhiteSpace(content) && file == null)
                return ApiResponse<MessageDto>.Fail("Message must have content or a file.");

            var fileUrl = string.Empty;
            if (messageType != MessageType.Text && file is not null)
                fileUrl = await _fileStorage.UploadAsync(file, messageType.ToString() + "s");

            var conversation = await GetOrCreateConversationAsync(fromUserId, toUserId);

            var message = new Message
            {
                ConversationId = conversation.Id,
                SenderId = fromUserId,
                MessageType = messageType,
                ReplyToMessageId = replyToMessageId,
                Content = content,
                FileUrl = fileUrl,
            };

            if (file != null)
            {
                message.FileUrl = fileUrl;
            }
            await _unitOfWork.Messages.AddAsync(message);
            conversation.LastMessageId = message.Id;
            _unitOfWork.Conversations.Update(conversation);
            conversation.LastMessageId = message.Id;


            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Message {MessageId} sent from {FromUserId} to {ToUserId} in conversation {ConversationId}", message.Id, fromUserId, toUserId, conversation.Id);

            // Eagerly load the replied-to message if it exists, so we can map it.
            if (message.ReplyToMessageId.HasValue)
            {
                message.ReplyToMessage = await _unitOfWork.Messages
                    .Find(m => m.Id == message.ReplyToMessageId.Value)
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync();
            }

            var messageDto = _mapper.Map<MessageDto>(message);
            
            // The sender of the new message is the current user.
            // The sender of the replied-to message is mapped from the entity.
            messageDto.Sender = currentUserResponse.Data;

            return ApiResponse<MessageDto>.Success(messageDto, "Message sent.");
        }

        public async Task<ApiResponse<AgoraTokenDto>> GenerateVideoCallTokensAsync(Guid toUserId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<AgoraTokenDto>.Fail("User not authenticated.");

            var fromUserId = currentUserResponse.Data.Id;

            if (!await CanMessageUserAsync(fromUserId, toUserId))
                return ApiResponse<AgoraTokenDto>.Fail("You can only call users you are following.");

            // A unique channel name for the call between two users.
            var channelName = GetChannelName(fromUserId, toUserId);
            
            // In Agora, UIDs must be 32-bit unsigned integers. We can't use GUIDs directly.
            // A simple hash is not guaranteed to be unique, but for this scope it's a pragmatic approach.
            // For production, you might map GUIDs to integer UIDs in your database.
            var userUid = (uint)fromUserId.GetHashCode();

            var token = _agoraService.GenerateRtcToken(channelName, userUid);

            _logger.LogInformation("Generated RTC token for user {UserId} for channel {ChannelName}", fromUserId, channelName);

            // Here you would typically send a notification to the `toUserId` with the channel name
            // so they can join. This can be done via your NotificationService or a push notification.

            return ApiResponse<AgoraTokenDto>.Success(new AgoraTokenDto { Token = token, ChannelName = channelName });
        }

        public async Task<ApiResponse<IQueryable<ConversationDto>>> GetConversationsAsync()
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<IQueryable<ConversationDto>>.Fail("User not authenticated.");

            var userId = currentUserResponse.Data.Id;

            var conversationsQuery = _unitOfWork.Conversations
                .Find(c => c.Participants.Any(p => p.Id == userId))
                .Include(c => c.Participants)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .ThenInclude(m => m.Sender)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationDto
                {
                    Id = c.Id,
                    Participants = c.Participants.Select(p => _mapper.Map<UserDto>(p)).ToList(),
                    LastMessage = c.Messages.Any() ? _mapper.Map<MessageDto>(c.Messages.First()) : null,
                    UpdatedAt = c.UpdatedAt,
                    UnreadCount = c.Messages.Count(m => m.SenderId != userId && !m.IsRead)
                });

            return ApiResponse<IQueryable<ConversationDto>>.Success(conversationsQuery);
        }

        public async Task<ApiResponse<IQueryable<MessageDto>>> GetMessagesAsync(Guid conversationId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<IQueryable<MessageDto>>.Fail("User not authenticated.");

            var userId = currentUserResponse.Data.Id;

            var isParticipant = await _unitOfWork.Conversations
                .Find(c => c.Id == conversationId && c.Participants.Any(p => p.Id == userId))
                .AnyAsync();

            if (!isParticipant)
            {
                return ApiResponse<IQueryable<MessageDto>>.Fail("You are not a participant of this conversation.");
            }

            var messagesQuery = _unitOfWork.Messages
                .Find(m => m.ConversationId == conversationId)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedAt);

            return ApiResponse<IQueryable<MessageDto>>.Success(_mapper.ProjectTo<MessageDto>(messagesQuery));
        }

        public async Task<bool> CanMessageUserAsync(Guid fromUserId, Guid toUserId)
        {
            return await _unitOfWork.UserFollows
                .Find(f =>
                    (f.FollowerId == fromUserId && f.FollowingId == toUserId) ||
                    (f.FollowerId == toUserId && f.FollowingId == fromUserId)
                )
                .AnyAsync();
        }


        public bool CanReply(Guid senderId, Guid recipientId)
        {
            return true; // allow reply regardless of follow status
        }

        private async Task<Conversation> GetOrCreateConversationAsync(Guid userId1, Guid userId2)
        {
            // Note: This query is complex. It finds a conversation containing BOTH users.
            var conversation = await _unitOfWork.Conversations
                .Find(c => c.Participants.Any(p => p.Id == userId1) && c.Participants.Any(p => p.Id == userId2))
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                var user1 = await _unitOfWork.Users.GetByIdAsync(userId1);
                var user2 = await _unitOfWork.Users.GetByIdAsync(userId2);
                conversation = new Conversation { Participants = [user1!, user2!] };
                await _unitOfWork.Conversations.AddAsync(conversation);
                await _unitOfWork.CompleteAsync();
            }
            return conversation;
        }

        private static string GetChannelName(Guid userId1, Guid userId2)
        {
            // Create a consistent, unique channel name for any pair of users.
            return string.Compare(userId1.ToString(), userId2.ToString(), StringComparison.Ordinal) < 0
                ? $"{userId1}_{userId2}"
                : $"{userId2}_{userId1}";
        }

        public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid messageId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<bool>.Fail("User not authenticated.");

            var userId = currentUserResponse.Data.Id;

            var message = await _unitOfWork.Messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
            if (message == null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.SenderId != userId) return ApiResponse<bool>.Fail("You cannot mark this message.");

            message.IsRead = true;
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("User {UserId} marked message {MessageId} as read", userId, messageId);
            return ApiResponse<bool>.Success(true, "Message marked as read.");
        }

        public async Task<ApiResponse<bool>> DeleteMessageAsync(Guid messageId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<bool>.Fail("User not authenticated.");

            var userId = currentUserResponse.Data.Id;

            var message = await _unitOfWork.Messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
            if (message == null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.SenderId != userId) return ApiResponse<bool>.Fail("You cannot delete this message.");

            var now = DateTime.UtcNow;
            if (now - message.CreatedAt <= TimeSpan.FromMinutes(2))
            { 
                message.IsDeleted = true;
                _logger.LogInformation("User {UserId} marked message {MessageId} as deleted (past 2 minutes)", userId, messageId);
            }

            await _unitOfWork.CompleteAsync();
            return ApiResponse<bool>.Success(true, "Message deletion processed.");
        }

        public async Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid conversationId)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
                return ApiResponse<bool>.Fail("User not authenticated.");

            var userId = currentUserResponse.Data.Id;

            var messages = await _unitOfWork.Messages
                .Find(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            if (!messages.Any()) return ApiResponse<bool>.Success(true, "No messages to mark as read.");

            foreach (var message in messages)
            {
                message.IsRead = true;
            }

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("User {UserId} marked all messages in conversation {ConversationId} as read", userId, conversationId);
            return ApiResponse<bool>.Success(true, "All messages marked as read.");
        }

        public async Task<ApiResponse<MessageDto?>> GetMessageByIdAsync(Guid messageId)
        {
            _logger.LogInformation("Fetching message with ID {MessageId}", messageId);

            try
            {
                var message = await _unitOfWork.Messages.Find(a => a.Id == messageId).Include(a => a.Sender)
                              .Select(a => new
                              {
                                  Message = a,
                                  SenderId = a.Sender.Id,
                                  SenderFullname = a.Sender.FullName,
                              }).FirstOrDefaultAsync();

                if (message is null)
                {
                    _logger.LogWarning("Message with ID {MessageId} was not found.", messageId);
                    return ApiResponse<MessageDto?>.Fail("Message not found.");
                }

                var dto = _mapper.Map<MessageDto>(message.Message);

                dto.Sender = new UserDto
                {
                    Id = message.SenderId,
                    FullName = message.SenderFullname,
                };

                _logger.LogInformation("Successfully retrieved message with ID {MessageId}.", messageId);

                return ApiResponse<MessageDto?>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "An error occurred while fetching message with ID {MessageId}.",
                    messageId);

                return ApiResponse<MessageDto?>.Fail("An error occurred.");
            }
        }

    }
}