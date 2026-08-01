using System.Text.Json;
using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Storage;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    public class GroupMessageService : IGroupMessageService
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorage _fileStorage;
        private readonly INotificationService _notificationService;
        private readonly GroupPermissionService _permissions;
        private readonly ITopicEventSender _eventSender;
        private readonly IMapper _mapper;
        private readonly ILogger<GroupMessageService> _logger;

        public GroupMessageService(
            IUnitOfWork unitOfWork,
            IFileStorage fileStorage,
            INotificationService notificationService,
            GroupPermissionService permissions,
            ITopicEventSender eventSender,
            IMapper mapper,
            ILogger<GroupMessageService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _notificationService = notificationService;
            _permissions = permissions;
            _eventSender = eventSender;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupMessageDto>> SendAsync(
            Guid groupId, Guid senderId, MessageType messageType, string? content, IFile? file, Guid? replyToMessageId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, senderId, GroupPermissions.CanSendMessage, ct))
                return ApiResponse<GroupMessageDto>.Fail("You are not a member of this group.");

            if (string.IsNullOrWhiteSpace(content) && file is null)
                return ApiResponse<GroupMessageDto>.Fail("Message must have content or a file.");
            if (messageType == MessageType.System)
                return ApiResponse<GroupMessageDto>.Fail("System messages cannot be created by users.");

            var members = await LoadMembersAsync(groupId, ct);
            var membersById = members.ToDictionary(m => m.UserId);

            string? fileUrl = null;
            if (file is not null)
                fileUrl = await _fileStorage.UploadAsync(file, messageType.ToString() + "s");

            GroupMessage? replyTo = null;
            if (replyToMessageId.HasValue)
            {
                replyTo = await _unitOfWork.GroupMessages.Find(m => m.Id == replyToMessageId.Value && m.GroupId == groupId).FirstOrDefaultAsync(ct);
                if (replyTo is null)
                    return ApiResponse<GroupMessageDto>.Fail("The message you are replying to was not found.");
            }

            var usernames = MentionParser.Parse(content);
            var mentioned = members.Where(m => usernames.Contains(m.User.Username, StringComparer.OrdinalIgnoreCase)).ToList();

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = senderId,
                MessageType = messageType,
                Content = content?.Trim(),
                FileUrl = fileUrl,
                ReplyToMessageId = replyToMessageId,
                Status = MessageStatus.Sent
            };

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null)
                return ApiResponse<GroupMessageDto>.Fail("Group not found.");

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _unitOfWork.GroupMessages.AddAsync(message);

                    foreach (var member in mentioned)
                    {
                        await _unitOfWork.GroupMessageMentions.AddAsync(new GroupMessageMention
                        {
                            MessageId = message.Id,
                            UserId = member.UserId,
                            MentionText = "@" + member.User.Username
                        });
                    }

                    group.LastMessageId = message.Id;
                    group.LastActivityAt = DateTime.UtcNow;
                    group.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.ChatGroups.Update(group);

                    foreach (var mentionedMember in mentioned)
                    {
                        if (mentionedMember.UserId == senderId || !ShouldNotify(membersById[mentionedMember.UserId]))
                            continue;
                        await CreateMessageNotificationAsync(mentionedMember.UserId, NotificationType.GroupMention, message, group, ct);
                    }

                    if (replyTo is not null && replyTo.SenderId != senderId && ShouldNotify(membersById[replyTo.SenderId]))
                    {
                        await CreateMessageNotificationAsync(replyTo.SenderId, NotificationType.GroupReply, message, group, ct);
                    }

                    await _unitOfWork.CompleteAsync(ct);
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send group message to group {GroupId}.", groupId);
                return ApiResponse<GroupMessageDto>.Fail("Failed to send message.");
            }

            var dto = await ToDtoAsync(message, group, members.Count, ct);
            await PublishAsync($"{groupId}_GroupMessage", dto, ct);
            return ApiResponse<GroupMessageDto>.Success(dto, "Message sent.");
        }

        public async Task<ApiResponse<GroupMessageDto>> EditAsync(Guid groupId, Guid messageId, Guid senderId, string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
                return ApiResponse<GroupMessageDto>.Fail("Message content is required.");

            var (message, group, members) = await LoadForOperationAsync(groupId, messageId, senderId, ct);
            if (message is null) return ApiResponse<GroupMessageDto>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<GroupMessageDto>.Fail("System messages cannot be edited.");
            if (message.SenderId != senderId) return ApiResponse<GroupMessageDto>.Fail("You can only edit your own messages.");

            message.Content = content.Trim();
            message.EditedAt = DateTime.UtcNow;
            message.EditedBy = senderId;
            _unitOfWork.GroupMessages.Update(message);

            try
            {
                await _unitOfWork.CompleteAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict editing group message {MessageId}.", messageId);
                return ApiResponse<GroupMessageDto>.Fail("This message was modified by someone else. Refresh and try again.");
            }

            var dto = await ToDtoAsync(message, group, members.Count, ct);
            await PublishAsync($"{groupId}_GroupMessageEdited", dto, ct);
            return ApiResponse<GroupMessageDto>.Success(dto, "Message edited.");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid groupId, Guid messageId, Guid senderId, CancellationToken ct = default)
        {
            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<bool>.Fail("System messages cannot be deleted.");
            if (message.SenderId != senderId) return ApiResponse<bool>.Fail("You can only delete your own messages.");

            message.Deleted = true;
            message.Content = null;
            message.FileUrl = null;
            _unitOfWork.GroupMessages.Update(message);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{groupId}_GroupMessageDeleted", message.Id, ct);
            return ApiResponse<bool>.Success(true, "Message deleted.");
        }

        public async Task<ApiResponse<GroupMessageDto>> SetPinnedAsync(Guid groupId, Guid messageId, Guid actorId, bool pin, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanPinMessage, ct))
                return ApiResponse<GroupMessageDto>.Fail("Only admins and the owner can pin messages.");

            var (message, group, members) = await LoadForOperationAsync(groupId, messageId, actorId, ct);
            if (message is null) return ApiResponse<GroupMessageDto>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<GroupMessageDto>.Fail("System messages cannot be pinned.");

            message.IsPinned = pin;
            message.PinnedAt = pin ? DateTime.UtcNow : null;
            message.PinnedBy = pin ? actorId : null;
            _unitOfWork.GroupMessages.Update(message);
            await _unitOfWork.CompleteAsync(ct);

            var dto = await ToDtoAsync(message, group, members.Count, ct);
            await PublishAsync($"{groupId}_GroupMessagePinned", dto, ct);
            return ApiResponse<GroupMessageDto>.Success(dto, pin ? "Message pinned." : "Message unpinned.");
        }

        public async Task<ApiResponse<bool>> ToggleReactionAsync(Guid groupId, Guid messageId, Guid userId, string emoji, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(emoji))
                return ApiResponse<bool>.Fail("Emoji is required.");

            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<bool>.Fail("System messages cannot be reacted to.");
            if (!await _permissions.IsMemberAsync(groupId, userId, ct)) return ApiResponse<bool>.Fail("You are not a member of this group.");

            var existing = await _unitOfWork.Reactions
                .Find(r => r.GroupMessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                if (existing.Emoji == emoji)
                {
                    _unitOfWork.Reactions.Remove(existing);
                    await _unitOfWork.CompleteAsync(ct);
                    await PublishAsync($"{groupId}_GroupMessageReactionRemoved", messageId, ct);
                    return ApiResponse<bool>.Success(false, "Reaction removed.");
                }

                existing.Emoji = emoji;
                _unitOfWork.Reactions.Update(existing);
                await _unitOfWork.CompleteAsync(ct);
                await PublishAsync($"{groupId}_GroupMessageReactionAdded", messageId, ct);
                return ApiResponse<bool>.Success(true, "Reaction changed.");
            }

            await _unitOfWork.Reactions.AddAsync(new Reaction
            {
                GroupMessageId = messageId,
                UserId = userId,
                Emoji = emoji
            });
            await _unitOfWork.CompleteAsync(ct);

            var members = await LoadMembersAsync(groupId, ct);
            var reactor = members.FirstOrDefault(m => m.UserId == userId);
            await PublishAsync($"{groupId}_GroupMessageReactionAdded", messageId, ct);
            return ApiResponse<bool>.Success(true, "Reaction added.");
        }

        public async Task<ApiResponse<bool>> RemoveReactionAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            var reaction = await _unitOfWork.Reactions
                .Find(r => r.GroupMessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);
            if (reaction is null) return ApiResponse<bool>.Fail("Reaction not found.");

            _unitOfWork.Reactions.Remove(reaction);
            await _unitOfWork.CompleteAsync(ct);
            await PublishAsync($"{groupId}_GroupMessageReactionRemoved", messageId, ct);
            return ApiResponse<bool>.Success(true, "Reaction removed.");
        }

        public async Task<ApiResponse<bool>> MarkDeliveredAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.SenderId == userId) return ApiResponse<bool>.Success(true, "Nothing to do.");

            var read = await _unitOfWork.GroupMessageReads
                .Find(r => r.MessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (read is null)
            {
                read = new GroupMessageRead { MessageId = messageId, UserId = userId, DeliveredAt = DateTime.UtcNow };
                await _unitOfWork.GroupMessageReads.AddAsync(read);
            }
            else
            {
                read.DeliveredAt ??= DateTime.UtcNow;
                _unitOfWork.GroupMessageReads.Update(read);
            }
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Message marked delivered.");
        }

        public async Task<ApiResponse<bool>> MarkReadAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.SenderId == userId) return ApiResponse<bool>.Success(true, "Nothing to do.");

            var now = DateTime.UtcNow;
            var read = await _unitOfWork.GroupMessageReads
                .Find(r => r.MessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (read is null)
            {
                await _unitOfWork.GroupMessageReads.AddAsync(new GroupMessageRead { MessageId = messageId, UserId = userId, DeliveredAt = now, ReadAt = now });
            }
            else
            {
                read.ReadAt = now;
                read.DeliveredAt ??= now;
                _unitOfWork.GroupMessageReads.Update(read);
            }
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Message marked read.");
        }

        public async Task<ApiResponse<bool>> MarkAllReadAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");

            membership.LastReadAt = DateTime.UtcNow;
            _unitOfWork.ChatGroupMembers.Update(membership);

            var unread = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && m.SenderId != userId && !m.Deleted)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            foreach (var message in unread)
            {
                var existing = await _unitOfWork.GroupMessageReads
                    .Find(r => r.MessageId == message.Id && r.UserId == userId)
                    .FirstOrDefaultAsync(ct);
                if (existing is null)
                {
                    await _unitOfWork.GroupMessageReads.AddAsync(new GroupMessageRead { MessageId = message.Id, UserId = userId, DeliveredAt = now, ReadAt = now });
                }
                else
                {
                    existing.ReadAt = now;
                    existing.DeliveredAt ??= now;
                    _unitOfWork.GroupMessageReads.Update(existing);
                }
            }

            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "All messages marked read.");
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId)
                .OrderByDescending(m => m.CreatedAt);

            var total = await query.CountAsync(ct);
            var messages = await query
                .Include(m => m.Sender)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<GroupMessageDto>> GetMessageAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<GroupMessageDto>.Fail("You are not a member of this group.");

            var message = await _unitOfWork.GroupMessages
                .Find(m => m.Id == messageId && m.GroupId == groupId)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<GroupMessageDto>.Fail("Message not found.");

            var members = await LoadMembersAsync(groupId, ct);
            var dto = await ToDtoAsync(message, message.Group ?? await _unitOfWork.ChatGroups.GetByIdAsync(groupId), members.Count, ct);
            return ApiResponse<GroupMessageDto>.Success(dto);
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetPinnedMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && m.IsPinned)
                .OrderByDescending(m => m.PinnedAt);

            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> SearchAsync(Guid groupId, Guid userId, GroupMessageSearchInput input, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            (var page, var pageSize) = Normalize(input.Page, input.PageSize);
            var query = _unitOfWork.GroupMessages.Find(m => m.GroupId == groupId && !m.Deleted);

            if (!string.IsNullOrWhiteSpace(input.Text))
                query = query.Where(m => m.Content != null && EF.Functions.Like(m.Content, $"%{input.Text.Trim()}%"));
            if (input.SenderId.HasValue)
                query = query.Where(m => m.SenderId == input.SenderId.Value);
            if (input.MentionedUserId.HasValue)
                query = query.Where(m => m.Mentions.Any(mn => mn.UserId == input.MentionedUserId.Value));
            if (input.Pinned.HasValue)
                query = query.Where(m => m.IsPinned == input.Pinned.Value);
            if (input.MediaType.HasValue)
                query = query.Where(m => m.MessageType == input.MediaType.Value);
            if (input.DateFrom.HasValue)
                query = query.Where(m => m.CreatedAt >= input.DateFrom.Value);
            if (input.DateTo.HasValue)
                query = query.Where(m => m.CreatedAt <= input.DateTo.Value);
            if (input.HasReactions.HasValue)
                query = input.HasReactions.Value
                    ? query.Where(m => m.Reactions.Any())
                    : query.Where(m => !m.Reactions.Any());
            if (input.RepliesOnly.HasValue && input.RepliesOnly.Value)
                query = query.Where(m => m.ReplyToMessageId != null);

            query = query.OrderByDescending(m => m.CreatedAt);
            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMediaAsync(Guid groupId, Guid userId, MessageType? mediaType, int page, int pageSize, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            var mediaTypes = new[] { MessageType.Image, MessageType.Video, MessageType.Document, MessageType.Audio };
            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && mediaTypes.Contains(m.MessageType) && !m.Deleted);

            if (mediaType.HasValue)
                query = query.Where(m => m.MessageType == mediaType.Value);

            query = query.OrderByDescending(m => m.CreatedAt);
            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<int>.Fail("You are not a member of this group.");

            var since = membership.LastReadAt;
            var count = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && m.SenderId != userId && !m.Deleted)
                .Where(m => since == null || m.CreatedAt > since.Value)
                .CountAsync(ct);

            return ApiResponse<int>.Success(count);
        }

        public async Task<ApiResponse<int>> GetUnreadGroupCountAsync(Guid userId, CancellationToken ct = default)
        {
            var counts = await GetUnreadCountsByGroupAsync(userId, ct);
            return ApiResponse<int>.Success(counts.Values.Sum());
        }

        public async Task<Dictionary<Guid, int>> GetUnreadCountsByGroupAsync(Guid userId, CancellationToken ct = default)
        {
            var query =
                from m in _unitOfWork.GroupMessages.GetAll()
                join mem in _unitOfWork.ChatGroupMembers.GetAll() on m.GroupId equals mem.GroupId
                where mem.UserId == userId && m.SenderId != userId && !m.Deleted
                      && (mem.LastReadAt == null || m.CreatedAt > mem.LastReadAt)
                group m by m.GroupId into g
                select new { GroupId = g.Key, Count = g.Count() };

            var rows = await query.ToListAsync(ct);
            return rows.ToDictionary(r => r.GroupId, r => r.Count);
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMyMentionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessageMentions
                .Find(mn => mn.UserId == userId)
                .Select(mn => mn.Message)
                .Distinct()
                .OrderByDescending(m => m.CreatedAt);

            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<GroupMessage> InsertSystemMessageAsync(ChatGroup group, Guid actorId, string content, string? metadata = null, CancellationToken ct = default)
        {
            var message = new GroupMessage
            {
                GroupId = group.Id,
                SenderId = actorId,
                MessageType = MessageType.System,
                Content = content,
                Metadata = metadata,
                Status = MessageStatus.Sent
            };

            await _unitOfWork.GroupMessages.AddAsync(message);
            group.LastMessageId = message.Id;
            group.LastActivityAt = DateTime.UtcNow;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{group.Id}_GroupMessage", ToMessageDto(message), ct);
            return message;
        }

        private async Task<(GroupMessage? Message, ChatGroup? Group, List<ChatGroupMember> Members)> LoadForOperationAsync(
            Guid groupId, Guid messageId, Guid actorId, CancellationToken ct)
        {
            var message = await _unitOfWork.GroupMessages
                .Find(m => m.Id == messageId && m.GroupId == groupId)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(ct);
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            var members = await LoadMembersAsync(groupId, ct);
            return (message, group, members);
        }

        private async Task<List<ChatGroupMember>> LoadMembersAsync(Guid groupId, CancellationToken ct) =>
            await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId)
                .Include(m => m.User)
                .ToListAsync(ct);

        private async Task CreateMessageNotificationAsync(
            Guid userId, NotificationType type, GroupMessage message, ChatGroup group, CancellationToken ct)
        {
            var metadata = JsonSerializer.Serialize(new
            {
                groupId = group.Id,
                groupName = group.Name,
                messageId = message.Id,
                senderId = message.SenderId,
                preview = message.Content ?? message.MessageType.ToString()
            });

            await _notificationService.CreateAsync(
                userId,
                type,
                $"{type}: {group.Name}",
                message.Id,
                (int)type,
                metadata,
                ct);
        }

        private static bool ShouldNotify(ChatGroupMember member) =>
            member.NotificationLevel is NotificationLevel.All or NotificationLevel.MentionsOnly &&
            (!member.Muted || member.MutedUntil == null || member.MutedUntil.Value <= DateTime.UtcNow);

        private async Task<List<GroupMessageDto>> ToDtosAsync(List<GroupMessage> messages, CancellationToken ct)
        {
            if (messages.Count == 0) return [];

            var groupIds = messages.Select(m => m.GroupId).Distinct().ToList();
            var memberCounts = await _unitOfWork.ChatGroupMembers
                .Find(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var countsByGroup = memberCounts.ToDictionary(c => c.GroupId, c => c.Count);

            var messageIds = messages.Select(m => m.Id).ToList();
            var reads = await _unitOfWork.GroupMessageReads
                .Find(r => messageIds.Contains(r.MessageId))
                .ToListAsync(ct);

            var dtos = messages.Select(m => ToMessageDto(m)).ToList();
            foreach (var dto in dtos)
            {
                var messageReads = reads.Where(r => r.MessageId == dto.Id).ToList();
                dto.DeliveredCount = messageReads.Count(r => r.DeliveredAt != null);
                dto.ReadCount = messageReads.Count(r => r.ReadAt != null);
                dto.UnreadCount = Math.Max(0, countsByGroup.GetValueOrDefault(dto.GroupId) - dto.ReadCount);
            }

            return dtos;
        }

        private async Task<GroupMessageDto> ToDtoAsync(GroupMessage message, ChatGroup? group, int memberCount, CancellationToken ct)
        {
            var dtos = await ToDtosAsync([message], ct);
            return dtos.FirstOrDefault() ?? ToMessageDto(message);
        }

        private GroupMessageDto ToMessageDto(GroupMessage message) => _mapper.Map<GroupMessageDto>(message);

        private static (int Page, int PageSize) Normalize(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;
            return (page, pageSize);
        }

        private async Task PublishAsync<T>(string topic, T payload, CancellationToken ct)
        {
            try
            {
                await _eventSender.SendAsync(topic, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event to topic {Topic}.", topic);
            }
        }
    }
}
