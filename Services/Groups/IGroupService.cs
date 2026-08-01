using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Storage;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupService
    {
        Task<ApiResponse<GroupDto>> CreateGroupAsync(Guid ownerId, string name, string? description, bool isPrivate, int? maxMembers, string? imageUrl, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> UpdateGroupAsync(Guid groupId, Guid actorId, string? name, string? description, bool? isPrivate, bool? archived, int? maxMembers, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> UploadGroupImageAsync(Guid groupId, Guid actorId, IFile file, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeleteGroupAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> TransferOwnershipAsync(Guid groupId, Guid actorId, Guid targetUserId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(Guid userId, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> GetGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> AddMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RemoveMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> LeaveGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> PromoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> DemoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetMembersAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<string>> GenerateInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RevokeInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> JoinByInviteAsync(string inviteCode, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RequestJoinAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ApproveJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RejectJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupJoinRequestDto>>> GetPendingJoinRequestsAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<string>> GetInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MuteGroupAsync(Guid groupId, Guid userId, DateTime? mutedUntil, CancellationToken ct = default);
        Task<ApiResponse<bool>> SetNotificationLevelAsync(Guid groupId, Guid userId, NotificationLevel level, CancellationToken ct = default);
    }
}
