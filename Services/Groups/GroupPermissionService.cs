using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    /// <summary>
    /// Shared membership/permission helper used by every group service so checks
    /// are never scattered. Rule functions live in <see cref="GroupPermissions"/>.
    /// </summary>
    public class GroupPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public GroupPermissionService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<ChatGroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct = default) =>
            await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId && m.UserId == userId)
                .FirstOrDefaultAsync(ct);

        public async Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default) =>
            await GetMembershipAsync(groupId, userId, ct) is not null;

        public async Task<bool> CanAsync(Guid groupId, Guid userId, Func<GroupMemberRole, bool> rule, CancellationToken ct = default)
        {
            var membership = await GetMembershipAsync(groupId, userId, ct);
            return membership is not null && rule(membership.Role);
        }
    }
}
