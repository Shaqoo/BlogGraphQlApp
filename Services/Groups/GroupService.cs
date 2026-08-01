using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITopicEventSender _eventSender;
        private readonly ILogger<GroupService> _logger;

        public GroupService(IUnitOfWork unitOfWork, ITopicEventSender eventSender, ILogger<GroupService> logger)
        {
            _unitOfWork = unitOfWork;
            _eventSender = eventSender;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupDto>> CreateGroupAsync(Guid ownerId, string name, string? imageUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ApiResponse<GroupDto>.Fail("Group name is required.");

            var group = new ChatGroup
            {
                Name = name.Trim(),
                CreatedBy = ownerId,
                ImageUrl = imageUrl
            };

            await _unitOfWork.ChatGroups.AddAsync(group);

            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = group.Id,
                UserId = ownerId,
                Role = GroupMemberRole.Owner
            });

            await _unitOfWork.CompleteAsync(cancellationToken);

            _logger.LogInformation("Group {GroupId} created by {UserId}.", group.Id, ownerId);
            return ApiResponse<GroupDto>.Success(new GroupDto
            {
                Id = group.Id,
                Name = group.Name,
                ImageUrl = group.ImageUrl,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                MemberCount = 1
            }, "Group created.");
        }

        public async Task<ApiResponse<GroupDto>> UpdateGroupAsync(Guid groupId, Guid actorId, string name, string? imageUrl, CancellationToken cancellationToken = default)
        {
            var (group, membership) = await LoadGroupAndMembershipAsync(groupId, actorId, cancellationToken);
            if (group is null)
                return ApiResponse<GroupDto>.Fail("Group not found.");
            if (membership is null)
                return ApiResponse<GroupDto>.Fail("You are not a member of this group.");
            if (!GroupPermissions.CanUpdateGroup(membership.Role))
                return ApiResponse<GroupDto>.Fail("You do not have permission to update this group.");

            if (string.IsNullOrWhiteSpace(name))
                return ApiResponse<GroupDto>.Fail("Group name is required.");

            group.Name = name.Trim();
            if (imageUrl is not null)
                group.ImageUrl = imageUrl;

            _unitOfWork.ChatGroups.Update(group);
            await _unitOfWork.CompleteAsync(cancellationToken);

            return ApiResponse<GroupDto>.Success(ToGroupDto(group, await MemberCountAsync(groupId, cancellationToken)), "Group updated.");
        }

        public async Task<ApiResponse<bool>> DeleteGroupAsync(Guid groupId, Guid actorId, CancellationToken cancellationToken = default)
        {
            var (group, membership) = await LoadGroupAndMembershipAsync(groupId, actorId, cancellationToken);
            if (group is null)
                return ApiResponse<bool>.Fail("Group not found.");
            if (membership is null)
                return ApiResponse<bool>.Fail("You are not a member of this group.");
            if (!GroupPermissions.CanDeleteGroup(membership.Role))
                return ApiResponse<bool>.Fail("Only the group owner can delete the group.");

            _unitOfWork.ChatGroups.Remove(group);
            await _unitOfWork.CompleteAsync(cancellationToken);

            _logger.LogInformation("Group {GroupId} deleted by {UserId}.", groupId, actorId);
            return ApiResponse<bool>.Success(true, "Group deleted.");
        }

        public async Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var memberships = await _unitOfWork.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Include(m => m.Group).ThenInclude(g => g.CreatedByUser)
                .OrderByDescending(m => m.JoinedAt)
                .ToListAsync(cancellationToken);

            var dtos = new List<GroupDto>();
            foreach (var membership in memberships)
            {
                var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == membership.GroupId);
                dtos.Add(ToGroupDto(membership.Group, count));
            }

            return ApiResponse<IEnumerable<GroupDto>>.Success(dtos);
        }

        public async Task<ApiResponse<GroupDto>> GetGroupAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        {
            var group = await _unitOfWork.ChatGroups
                .Find(g => g.Id == groupId)
                .Include(g => g.CreatedByUser)
                .FirstOrDefaultAsync(cancellationToken);

            if (group is null)
                return ApiResponse<GroupDto>.Fail("Group not found.");

            var membership = await GetMembershipAsync(groupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<GroupDto>.Fail("You are not a member of this group.");

            var count = await MemberCountAsync(groupId, cancellationToken);
            return ApiResponse<GroupDto>.Success(ToGroupDto(group, count));
        }

        public async Task<ApiResponse<bool>> AddMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default)
        {
            var (group, membership) = await LoadGroupAndMembershipAsync(groupId, actorId, cancellationToken);
            if (group is null)
                return ApiResponse<bool>.Fail("Group not found.");
            if (membership is null)
                return ApiResponse<bool>.Fail("You are not a member of this group.");
            if (!GroupPermissions.CanAddMember(membership.Role))
                return ApiResponse<bool>.Fail("You do not have permission to add members.");

            var target = await _unitOfWork.Users.GetByIdAsync(userId);
            if (target is null)
                return ApiResponse<bool>.Fail("User not found.");

            var alreadyMember = await _unitOfWork.ChatGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (alreadyMember)
                return ApiResponse<bool>.Fail("User is already a member of this group.");

            if (!await AreFriendsAsync(actorId, userId))
                return ApiResponse<bool>.Fail("You can only add friends to a group.");

            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = groupId,
                UserId = userId,
                Role = GroupMemberRole.Member
            });
            await _unitOfWork.CompleteAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Member added.");
        }

        public async Task<ApiResponse<bool>> RemoveMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default)
        {
            var (group, membership) = await LoadGroupAndMembershipAsync(groupId, actorId, cancellationToken);
            if (group is null)
                return ApiResponse<bool>.Fail("Group not found.");
            if (membership is null)
                return ApiResponse<bool>.Fail("You are not a member of this group.");

            var targetMembership = await GetMembershipAsync(groupId, userId, cancellationToken);
            if (targetMembership is null)
                return ApiResponse<bool>.Fail("User is not a member of this group.");

            if (!GroupPermissions.CanRemoveMember(membership.Role, targetMembership.Role))
                return ApiResponse<bool>.Fail("You do not have permission to remove this member.");

            _unitOfWork.ChatGroupMembers.Remove(targetMembership);
            await _unitOfWork.CompleteAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Member removed.");
        }

        public async Task<ApiResponse<bool>> LeaveGroupAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await GetMembershipAsync(groupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<bool>.Fail("You are not a member of this group.");

            if (membership.Role == GroupMemberRole.Owner)
                return ApiResponse<bool>.Fail("The owner cannot leave; delete the group instead.");

            _unitOfWork.ChatGroupMembers.Remove(membership);
            await _unitOfWork.CompleteAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "You left the group.");
        }

        public async Task<ApiResponse<bool>> PromoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await ChangeRoleAsync(groupId, actorId, userId, GroupMemberRole.Admin, promote: true, cancellationToken);
        }

        public async Task<ApiResponse<bool>> DemoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await ChangeRoleAsync(groupId, actorId, userId, GroupMemberRole.Member, promote: false, cancellationToken);
        }

        public async Task<ApiResponse<GroupMessageDto>> SendMessageAsync(Guid groupId, Guid senderId, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ApiResponse<GroupMessageDto>.Fail("Message text is required.");

            var membership = await GetMembershipAsync(groupId, senderId, cancellationToken);
            if (membership is null)
                return ApiResponse<GroupMessageDto>.Fail("You are not a member of this group.");
            if (!GroupPermissions.CanSendMessage(membership.Role))
                return ApiResponse<GroupMessageDto>.Fail("You do not have permission to send messages.");

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = senderId,
                Content = text.Trim()
            };

            await _unitOfWork.GroupMessages.AddAsync(message);
            await _unitOfWork.CompleteAsync(cancellationToken);

            var sender = await _unitOfWork.Users.GetByIdAsync(senderId);
            var dto = ToMessageDto(message, sender);
            await PublishAsync($"{groupId}_GroupMessage", dto, cancellationToken);

            return ApiResponse<GroupMessageDto>.Success(dto, "Message sent.");
        }

        public async Task<ApiResponse<IEnumerable<GroupMessageDto>>> GetMessagesAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await GetMembershipAsync(groupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<IEnumerable<GroupMessageDto>>.Fail("You are not a member of this group.");

            var messages = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId)
                .Include(m => m.Sender)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);

            var dtos = messages.Select(m => ToMessageDto(m, m.Sender)).ToList();
            return ApiResponse<IEnumerable<GroupMessageDto>>.Success(dtos);
        }

        public async Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetMembersAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = await GetMembershipAsync(groupId, userId, cancellationToken);
            if (membership is null)
                return ApiResponse<IEnumerable<GroupMemberDto>>.Fail("You are not a member of this group.");

            var members = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId)
                .Include(m => m.User)
                .OrderBy(m => m.JoinedAt)
                .ToListAsync(cancellationToken);

            var dtos = members.Select(m => new GroupMemberDto
            {
                Id = m.Id,
                GroupId = m.GroupId,
                UserId = m.UserId,
                Username = m.User.Username,
                FullName = m.User.FullName,
                Avatar = m.User.ProfilePictureUrl,
                Role = m.Role.ToString(),
                JoinedAt = m.JoinedAt
            }).ToList();

            return ApiResponse<IEnumerable<GroupMemberDto>>.Success(dtos);
        }

        private async Task<ApiResponse<bool>> ChangeRoleAsync(
            Guid groupId,
            Guid actorId,
            Guid userId,
            GroupMemberRole newRole,
            bool promote,
            CancellationToken cancellationToken)
        {
            var (group, membership) = await LoadGroupAndMembershipAsync(groupId, actorId, cancellationToken);
            if (group is null)
                return ApiResponse<bool>.Fail("Group not found.");
            if (membership is null)
                return ApiResponse<bool>.Fail("You are not a member of this group.");

            var can = promote ? GroupPermissions.CanPromoteAdmin(membership.Role) : GroupPermissions.CanDemoteAdmin(membership.Role);
            if (!can)
                return ApiResponse<bool>.Fail("Only the group owner can change member roles.");

            var target = await GetMembershipAsync(groupId, userId, cancellationToken);
            if (target is null)
                return ApiResponse<bool>.Fail("User is not a member of this group.");

            if (target.Role == GroupMemberRole.Owner)
                return ApiResponse<bool>.Fail("The group owner's role cannot be changed.");

            if (promote && target.Role == GroupMemberRole.Admin)
                return ApiResponse<bool>.Fail("User is already an admin.");
            if (!promote && target.Role == GroupMemberRole.Member)
                return ApiResponse<bool>.Fail("User is already a regular member.");

            target.Role = newRole;
            _unitOfWork.ChatGroupMembers.Update(target);
            await _unitOfWork.CompleteAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, promote ? "Member promoted to admin." : "Admin demoted to member.");
        }

        private async Task<(ChatGroup? Group, ChatGroupMember? Membership)> LoadGroupAndMembershipAsync(
            Guid groupId, Guid userId, CancellationToken cancellationToken)
        {
            var group = await _unitOfWork.ChatGroups
                .Find(g => g.Id == groupId)
                .Include(g => g.CreatedByUser)
                .FirstOrDefaultAsync(cancellationToken);
            var membership = group is null ? null : await GetMembershipAsync(groupId, userId, cancellationToken);
            return (group, membership);
        }

        private async Task<ChatGroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken cancellationToken) =>
            await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId && m.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

        private async Task<int> MemberCountAsync(Guid groupId, CancellationToken cancellationToken) =>
            await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == groupId);

        private async Task<bool> AreFriendsAsync(Guid a, Guid b) =>
            await _unitOfWork.UserFollows.AnyAsync(f => f.FollowerId == a && f.FollowingId == b) &&
            await _unitOfWork.UserFollows.AnyAsync(f => f.FollowerId == b && f.FollowingId == a);

        private async Task PublishAsync(string topic, object payload, CancellationToken cancellationToken)
        {
            try
            {
                await _eventSender.SendAsync(topic, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event to topic {Topic}.", topic);
            }
        }

        private static GroupDto ToGroupDto(ChatGroup group, int memberCount) => new()
        {
            Id = group.Id,
            Name = group.Name,
            ImageUrl = group.ImageUrl,
            CreatedBy = group.CreatedBy,
            CreatedByName = group.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = group.CreatedAt,
            MemberCount = memberCount
        };

        private static GroupMessageDto ToMessageDto(GroupMessage message, Models.User? sender) => new()
        {
            Id = message.Id,
            GroupId = message.GroupId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? string.Empty,
            SenderAvatar = sender?.ProfilePictureUrl,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            EditedAt = message.EditedAt,
            Deleted = message.Deleted
        };
    }
}
