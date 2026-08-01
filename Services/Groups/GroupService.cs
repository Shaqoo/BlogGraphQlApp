using System.Text.Json;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Implementations;
using BlogGraphQlApp.Storage;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GroupPermissionService _permissions;
        private readonly IGroupMessageService _messageService;
        private readonly INotificationService _notificationService;
        private readonly IFileStorage _fileStorage;
        private readonly PresenceTracker _presence;
        private readonly ITopicEventSender _eventSender;
        private readonly ILogger<GroupService> _logger;

        public GroupService(
            IUnitOfWork unitOfWork,
            GroupPermissionService permissions,
            IGroupMessageService messageService,
            INotificationService notificationService,
            IFileStorage fileStorage,
            PresenceTracker presence,
            ITopicEventSender eventSender,
            ILogger<GroupService> logger)
        {
            _unitOfWork = unitOfWork;
            _permissions = permissions;
            _messageService = messageService;
            _notificationService = notificationService;
            _fileStorage = fileStorage;
            _presence = presence;
            _eventSender = eventSender;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupDto>> CreateGroupAsync(Guid ownerId, string name, string? description, bool isPrivate, int? maxMembers, string? imageUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ApiResponse<GroupDto>.Fail("Group name is required.");

            var group = new ChatGroup
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                ImageUrl = imageUrl,
                IsPrivate = isPrivate,
                MaxMembers = maxMembers,
                CreatedBy = ownerId,
                InviteCode = GenerateInviteCode(),
                UpdatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroups.AddAsync(group);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = group.Id,
                UserId = ownerId,
                Role = GroupMemberRole.Owner,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Group {GroupId} created by {UserId}.", group.Id, ownerId);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, ownerId, ct), "Group created.");
        }

        public async Task<ApiResponse<GroupDto>> UpdateGroupAsync(Guid groupId, Guid actorId, string? name, string? description, bool? isPrivate, bool? archived, int? maxMembers, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanUpdateGroup, ct))
                return ApiResponse<GroupDto>.Fail("You do not have permission to update this group.");

            var changes = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(name) && group.Name != name.Trim())
            {
                changes.Append($"Name changed to \"{name.Trim()}\". ");
                group.Name = name.Trim();
            }
            if (description is not null && group.Description != description.Trim())
            {
                changes.Append("Description changed. ");
                group.Description = description.Trim();
            }
            if (isPrivate.HasValue && group.IsPrivate != isPrivate.Value)
            {
                changes.Append($"Group is now {(isPrivate.Value ? "private" : "public")}. ");
                group.IsPrivate = isPrivate.Value;
            }
            if (archived.HasValue)
                group.Archived = archived.Value;
            if (maxMembers.HasValue)
                group.MaxMembers = maxMembers.Value;

            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);

            try
            {
                await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
                await _unitOfWork.CompleteAsync(ct);
                if (changes.Length > 0)
                    await _messageService.InsertSystemMessageAsync(group, actorId, changes.ToString().Trim(), JsonSerializer.Serialize(new { actorId }), ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating group {GroupId}.", groupId);
                return ApiResponse<GroupDto>.Fail("This group was modified by someone else. Refresh and try again.");
            }

            await PublishAsync($"{groupId}_GroupUpdated", await ToGroupDtoAsync(group, actorId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, actorId, ct), "Group updated.");
        }

        public async Task<ApiResponse<GroupDto>> UploadGroupImageAsync(Guid groupId, Guid actorId, IFile file, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanChangeImage, ct))
                return ApiResponse<GroupDto>.Fail("Only admins and the owner can change the group image.");

            var oldUrl = group.ImageUrl;
            var newUrl = await _fileStorage.UploadAsync(file, "groupimages");
            group.ImageUrl = newUrl;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, "Group image updated.", JsonSerializer.Serialize(new { actorId }), ct);
            await tx.CommitAsync(ct);

            if (oldUrl is not null)
                await _fileStorage.DeleteAsync(oldUrl);

            await PublishAsync($"{groupId}_GroupUpdated", await ToGroupDtoAsync(group, actorId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, actorId, ct), "Group image updated.");
        }

        public async Task<ApiResponse<bool>> DeleteGroupAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanDeleteGroup, ct))
                return ApiResponse<bool>.Fail("Only the group owner can delete the group.");

            _unitOfWork.ChatGroups.Remove(group);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Group deleted.");
        }

        public async Task<ApiResponse<GroupDto>> TransferOwnershipAsync(Guid groupId, Guid actorId, Guid targetUserId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanTransferOwnership, ct))
                return ApiResponse<GroupDto>.Fail("Only the owner can transfer ownership.");

            var target = await _permissions.GetMembershipAsync(groupId, targetUserId, ct);
            if (target is null) return ApiResponse<GroupDto>.Fail("Target user is not a member of this group.");
            if (target.UserId == actorId) return ApiResponse<GroupDto>.Fail("You already own this group.");

            var targetUser = await _unitOfWork.Users.GetByIdAsync(targetUserId);
            var actorMembership = await _permissions.GetMembershipAsync(groupId, actorId, ct);
            actorMembership!.Role = GroupMemberRole.Admin;
            target.Role = GroupMemberRole.Owner;
            group.CreatedBy = targetUserId;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroupMembers.Update(actorMembership);
            _unitOfWork.ChatGroupMembers.Update(target);
            _unitOfWork.ChatGroups.Update(group);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"Ownership transferred to {targetUser?.FullName ?? "a member"}.", JsonSerializer.Serialize(new { actorId, targetUserId }), ct);
            await _notificationService.CreateAsync(targetUserId, NotificationType.GroupRoleChanged, $"You are now the owner of {group.Name}.", group.Id, (int)NotificationType.GroupRoleChanged, null, ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupUpdated", await ToGroupDtoAsync(group, actorId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, actorId, ct), "Ownership transferred.");
        }

        public async Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(Guid userId, CancellationToken ct = default)
        {
            var memberships = await _unitOfWork.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Include(m => m.Group)
                .ToListAsync(ct);

            if (memberships.Count == 0)
                return ApiResponse<IEnumerable<GroupDto>>.Success([]);

            var groupIds = memberships.Select(m => m.GroupId).ToList();

            var lastMessages = await _unitOfWork.GroupMessages
                .Find(m => groupIds.Contains(m.GroupId))
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);
            var lastByGroup = lastMessages
                .GroupBy(m => m.GroupId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

            var memberCounts = await _unitOfWork.ChatGroupMembers
                .Find(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var counts = memberCounts.ToDictionary(c => c.GroupId, c => c.Count);

            var unread = await _messageService.GetUnreadCountsByGroupAsync(userId, ct);

            var dtos = new List<GroupDto>();
            foreach (var membership in memberships)
            {
                var last = lastByGroup.GetValueOrDefault(membership.GroupId);
                dtos.Add(ToGroupDto(
                    membership.Group,
                    counts.GetValueOrDefault(membership.GroupId),
                    unread.GetValueOrDefault(membership.GroupId),
                    last,
                    last?.Sender,
                    inviteCode: null));
            }

            return ApiResponse<IEnumerable<GroupDto>>.Success(dtos);
        }

        public async Task<ApiResponse<GroupDto>> GetGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups
                .Find(g => g.Id == groupId)
                .Include(g => g.CreatedByUser)
                .FirstOrDefaultAsync(ct);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<GroupDto>.Fail("You are not a member of this group.");

            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, userId, ct));
        }

        public async Task<ApiResponse<bool>> AddMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanAddMember, ct))
                return ApiResponse<bool>.Fail("You do not have permission to add members.");

            var target = await _unitOfWork.Users.GetByIdAsync(userId);
            if (target is null) return ApiResponse<bool>.Fail("User not found.");

            var alreadyMember = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (alreadyMember is not null) return ApiResponse<bool>.Fail("User is already a member of this group.");

            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == groupId);
            if (group.MaxMembers.HasValue && count >= group.MaxMembers.Value)
                return ApiResponse<bool>.Fail("The group has reached its member limit.");

            if (!await AreFriendsAsync(actorId, userId))
                return ApiResponse<bool>.Fail("You can only add friends to a group.");

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = groupId,
                UserId = userId,
                Role = GroupMemberRole.Member,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{target.FullName} added to the group.", JsonSerializer.Serialize(new { actorId, userId }), ct);
            await _notificationService.CreateAsync(userId, NotificationType.GroupMemberAdded, $"You were added to {group.Name}.", group.Id, (int)NotificationType.GroupMemberAdded, JsonSerializer.Serialize(new { groupId, groupName = group.Name, imageUrl = group.ImageUrl, addedBy = actorId }), ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberJoined", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<bool>.Success(true, "Member added.");
        }

        public async Task<ApiResponse<bool>> RemoveMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            var actorMembership = await _permissions.GetMembershipAsync(groupId, actorId, ct);
            var targetMembership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (actorMembership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");
            if (targetMembership is null) return ApiResponse<bool>.Fail("User is not a member of this group.");
            if (!GroupPermissions.CanRemoveMember(actorMembership.Role, targetMembership.Role))
                return ApiResponse<bool>.Fail("You do not have permission to remove this member.");

            var target = await _unitOfWork.Users.GetByIdAsync(userId);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            _unitOfWork.ChatGroupMembers.Remove(targetMembership);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{target?.FullName ?? "A member"} was removed from the group.", JsonSerializer.Serialize(new { actorId, userId }), ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberLeft", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<bool>.Success(true, "Member removed.");
        }

        public async Task<ApiResponse<bool>> LeaveGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");
            if (membership.Role == GroupMemberRole.Owner)
                return ApiResponse<bool>.Fail("The owner cannot leave; transfer ownership or delete the group.");

            var remainingMembers = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId && m.UserId != userId)
                .Include(m => m.User)
                .ToListAsync(ct);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            _unitOfWork.ChatGroupMembers.Remove(membership);
            await _unitOfWork.CompleteAsync(ct);

            if (remainingMembers.Count == 0)
            {
                group.Archived = true;
                group.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.ChatGroups.Update(group);
                await _unitOfWork.CompleteAsync(ct);
            }
            else
            {
                await _messageService.InsertSystemMessageAsync(group, userId, $"{user?.FullName ?? "A member"} left the group.", JsonSerializer.Serialize(new { userId }), ct);
                foreach (var remaining in remainingMembers)
                {
                    await _notificationService.CreateAsync(remaining.UserId, NotificationType.GroupUpdated, $"{user?.FullName ?? "A member"} left {group.Name}.", group.Id, (int)NotificationType.GroupUpdated, null, ct);
                }
            }
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberLeft", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<bool>.Success(true, "You left the group.");
        }

        public async Task<ApiResponse<bool>> PromoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
            => await ChangeRoleAsync(groupId, actorId, userId, GroupMemberRole.Admin, "promoted to admin", ct);

        public async Task<ApiResponse<bool>> DemoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
            => await ChangeRoleAsync(groupId, actorId, userId, GroupMemberRole.Member, "demoted to member", ct);

        public async Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetMembersAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<IEnumerable<GroupMemberDto>>.Fail("You are not a member of this group.");

            var members = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId)
                .Include(m => m.User)
                .OrderBy(m => m.JoinedAt)
                .ToListAsync(ct);

            var dtos = new List<GroupMemberDto>();
            foreach (var member in members)
            {
                dtos.Add(new GroupMemberDto
                {
                    Id = member.Id,
                    GroupId = member.GroupId,
                    UserId = member.UserId,
                    Username = member.User.Username,
                    FullName = member.User.FullName,
                    Avatar = member.User.ProfilePictureUrl,
                    Role = member.Role.ToString(),
                    JoinedAt = member.JoinedAt,
                    Online = await _presence.IsOnline(member.UserId),
                    LastSeen = member.User.LastSeen
                });
            }

            return ApiResponse<IEnumerable<GroupMemberDto>>.Success(dtos);
        }

        public async Task<ApiResponse<string>> GenerateInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageInvite, ct))
                return ApiResponse<string>.Fail("Only admins and the owner can manage the invite link.");

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<string>.Fail("Group not found.");

            group.InviteCode = GenerateInviteCode();
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, "Invite link regenerated.", JsonSerializer.Serialize(new { actorId }), ct);
            await tx.CommitAsync(ct);

            return ApiResponse<string>.Success(group.InviteCode, "Invite code generated.");
        }

        public async Task<ApiResponse<bool>> RevokeInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageInvite, ct))
                return ApiResponse<bool>.Fail("Only admins and the owner can manage the invite link.");

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            group.InviteCode = null;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Invite code revoked.");
        }

        public async Task<ApiResponse<GroupDto>> JoinByInviteAsync(string inviteCode, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups
                .Find(g => g.InviteCode == inviteCode)
                .FirstOrDefaultAsync(ct);
            if (group is null) return ApiResponse<GroupDto>.Fail("Invalid invite code.");
            if (group.IsPrivate) return ApiResponse<GroupDto>.Fail("This group is private; request to join instead.");
            if (await _permissions.GetMembershipAsync(group.Id, userId, ct) is not null)
                return ApiResponse<GroupDto>.Fail("You are already a member of this group.");

            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == group.Id);
            if (group.MaxMembers.HasValue && count >= group.MaxMembers.Value)
                return ApiResponse<GroupDto>.Fail("The group has reached its member limit.");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                Role = GroupMemberRole.Member,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, userId, $"{user?.FullName ?? "A member"} joined the group.", JsonSerializer.Serialize(new { userId }), ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{group.Id}_GroupMemberJoined", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, userId, ct), "Joined group.");
        }

        public async Task<ApiResponse<bool>> RequestJoinAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");
            if (!group.IsPrivate) return ApiResponse<bool>.Fail("This group is public; join directly.");

            var existing = await _unitOfWork.GroupJoinRequests
                .Find(r => r.GroupId == groupId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);
            if (existing is not null)
                return ApiResponse<bool>.Fail(existing.Status == JoinRequestStatus.Pending ? "Your request is pending." : "You have already requested to join this group.");

            await _unitOfWork.GroupJoinRequests.AddAsync(new GroupJoinRequest { GroupId = groupId, UserId = userId });
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Join request submitted.");
        }

        public async Task<ApiResponse<bool>> ApproveJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageJoinRequests, ct))
                return ApiResponse<bool>.Fail("Only admins and the owner can approve join requests.");

            var request = await _unitOfWork.GroupJoinRequests
                .Find(r => r.Id == requestId && r.GroupId == groupId)
                .Include(r => r.User)
                .FirstOrDefaultAsync(ct);
            if (request is null || request.Status != JoinRequestStatus.Pending)
                return ApiResponse<bool>.Fail("Join request not found.");

            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == groupId);
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group!.MaxMembers.HasValue && count >= group.MaxMembers.Value)
                return ApiResponse<bool>.Fail("The group has reached its member limit.");

            request.Status = JoinRequestStatus.Approved;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = actorId;
            _unitOfWork.GroupJoinRequests.Update(request);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = groupId,
                UserId = request.UserId,
                Role = GroupMemberRole.Member,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{request.User.FullName} joined the group.", JsonSerializer.Serialize(new { actorId, userId = request.UserId }), ct);
            await _notificationService.CreateAsync(request.UserId, NotificationType.GroupMemberAdded, $"Your request to join {group.Name} was approved.", group.Id, (int)NotificationType.GroupMemberAdded, null, ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberJoined", await ToMemberDtoAsync(request.UserId, ct), ct);
            return ApiResponse<bool>.Success(true, "Join request approved.");
        }

        public async Task<ApiResponse<bool>> RejectJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageJoinRequests, ct))
                return ApiResponse<bool>.Fail("Only admins and the owner can reject join requests.");

            var request = await _unitOfWork.GroupJoinRequests
                .Find(r => r.Id == requestId && r.GroupId == groupId)
                .FirstOrDefaultAsync(ct);
            if (request is null || request.Status != JoinRequestStatus.Pending)
                return ApiResponse<bool>.Fail("Join request not found.");

            request.Status = JoinRequestStatus.Rejected;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = actorId;
            _unitOfWork.GroupJoinRequests.Update(request);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Join request rejected.");
        }

        public async Task<ApiResponse<IEnumerable<GroupJoinRequestDto>>> GetPendingJoinRequestsAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageJoinRequests, ct))
                return ApiResponse<IEnumerable<GroupJoinRequestDto>>.Fail("Only admins and the owner can view join requests.");

            var requests = await _unitOfWork.GroupJoinRequests
                .Find(r => r.GroupId == groupId && r.Status == JoinRequestStatus.Pending)
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync(ct);

            var dtos = requests.Select(r => new GroupJoinRequestDto
            {
                Id = r.Id,
                GroupId = r.GroupId,
                UserId = r.UserId,
                Username = r.User.Username,
                FullName = r.User.FullName,
                Avatar = r.User.ProfilePictureUrl,
                Status = r.Status,
                RequestedAt = r.RequestedAt
            });
            return ApiResponse<IEnumerable<GroupJoinRequestDto>>.Success(dtos);
        }

        public async Task<ApiResponse<string>> GetInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageInvite, ct))
                return ApiResponse<string>.Fail("Only admins and the owner can view the invite code.");

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<string>.Fail("Group not found.");
            return ApiResponse<string>.Success(group.InviteCode ?? string.Empty);
        }

        public async Task<ApiResponse<bool>> MuteGroupAsync(Guid groupId, Guid userId, DateTime? mutedUntil, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");

            membership.Muted = mutedUntil is null;
            membership.MutedUntil = mutedUntil;
            _unitOfWork.ChatGroupMembers.Update(membership);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, mutedUntil is null ? "Group muted." : $"Group muted until {mutedUntil:u}.");
        }

        public async Task<ApiResponse<bool>> SetNotificationLevelAsync(Guid groupId, Guid userId, NotificationLevel level, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");

            membership.NotificationLevel = level;
            _unitOfWork.ChatGroupMembers.Update(membership);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Notification level updated.");
        }

        private async Task<ApiResponse<bool>> ChangeRoleAsync(Guid groupId, Guid actorId, Guid userId, GroupMemberRole newRole, string action, CancellationToken ct)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            var actorMembership = await _permissions.GetMembershipAsync(groupId, actorId, ct);
            if (actorMembership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");
            var can = newRole == GroupMemberRole.Admin
                ? GroupPermissions.CanPromoteAdmin(actorMembership.Role)
                : GroupPermissions.CanDemoteAdmin(actorMembership.Role);
            if (!can) return ApiResponse<bool>.Fail("Only the group owner can change member roles.");

            var target = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (target is null) return ApiResponse<bool>.Fail("User is not a member of this group.");
            if (target.Role == GroupMemberRole.Owner) return ApiResponse<bool>.Fail("The group owner's role cannot be changed.");
            if (target.Role == newRole)
                return ApiResponse<bool>.Fail(newRole == GroupMemberRole.Admin ? "User is already an admin." : "User is already a regular member.");

            var targetUser = await _unitOfWork.Users.GetByIdAsync(userId);

            target.Role = newRole;
            _unitOfWork.ChatGroupMembers.Update(target);
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{targetUser?.FullName ?? "A member"} was {action}.", JsonSerializer.Serialize(new { actorId, userId }), ct);
            await _notificationService.CreateAsync(userId, NotificationType.GroupRoleChanged, $"You were {action} in {group.Name}.", group.Id, (int)NotificationType.GroupRoleChanged, null, ct);
            await tx.CommitAsync(ct);
            return ApiResponse<bool>.Success(true, "Role updated.");
        }

        private async Task<GroupMemberDto> ToMemberDtoAsync(Guid userId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            return new GroupMemberDto
            {
                Id = Guid.Empty,
                UserId = userId,
                Username = user?.Username ?? string.Empty,
                FullName = user?.FullName ?? string.Empty,
                Avatar = user?.ProfilePictureUrl,
                Online = await _presence.IsOnline(userId),
                LastSeen = user?.LastSeen
            };
        }

        private async Task<GroupDto> ToGroupDtoAsync(ChatGroup group, Guid actorId, CancellationToken ct)
        {
            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == group.Id);
            var unread = await _messageService.GetUnreadCountsByGroupAsync(actorId, ct);
            var membership = await _permissions.GetMembershipAsync(group.Id, actorId, ct);
            var canViewInvite = membership is not null && GroupPermissions.CanManageInvite(membership.Role);

            var last = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == group.Id)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            return ToGroupDto(group, count, unread.GetValueOrDefault(group.Id), last, last?.Sender, canViewInvite ? group.InviteCode : null);
        }

        private static GroupDto ToGroupDto(ChatGroup group, int memberCount, int unreadCount, GroupMessage? lastMessage, Models.User? lastSender, string? inviteCode) => new()
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            ImageUrl = group.ImageUrl,
            IsPrivate = group.IsPrivate,
            InviteCode = inviteCode,
            LastMessageId = group.LastMessageId,
            LastMessage = lastMessage is null ? null : new GroupMessageDto
            {
                Id = lastMessage.Id,
                GroupId = lastMessage.GroupId,
                SenderId = lastMessage.SenderId,
                SenderName = lastMessage.Sender?.FullName ?? string.Empty,
                MessageType = lastMessage.MessageType,
                Content = lastMessage.Content,
                FileUrl = lastMessage.FileUrl,
                CreatedAt = lastMessage.CreatedAt,
                Deleted = lastMessage.Deleted
            },
            LastSender = lastSender is null ? null : new UserDto { Id = lastSender.Id, FullName = lastSender.FullName, Username = lastSender.Username, ProfilePictureUrl = lastSender.ProfilePictureUrl },
            LastActivityAt = group.LastActivityAt,
            UpdatedAt = group.UpdatedAt,
            Archived = group.Archived,
            MaxMembers = group.MaxMembers,
            CreatedBy = group.CreatedBy,
            CreatedByName = group.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = group.CreatedAt,
            MemberCount = memberCount,
            UnreadCount = unreadCount
        };

        private static string GenerateInviteCode() => Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12].ToLowerInvariant();

        private async Task<bool> AreFriendsAsync(Guid a, Guid b) =>
            await _unitOfWork.UserFollows.AnyAsync(f => f.FollowerId == a && f.FollowingId == b) &&
            await _unitOfWork.UserFollows.AnyAsync(f => f.FollowerId == b && f.FollowingId == a);

        private async Task PublishAsync(string topic, object payload, CancellationToken ct)
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
