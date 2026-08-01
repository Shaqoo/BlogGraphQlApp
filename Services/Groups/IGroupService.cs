using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupService
    {
        Task<ApiResponse<GroupDto>> CreateGroupAsync(Guid ownerId, string name, string? imageUrl, CancellationToken cancellationToken = default);
        Task<ApiResponse<GroupDto>> UpdateGroupAsync(Guid groupId, Guid actorId, string name, string? imageUrl, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> DeleteGroupAsync(Guid groupId, Guid actorId, CancellationToken cancellationToken = default);
        Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<GroupDto>> GetGroupAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> AddMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> RemoveMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> LeaveGroupAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> PromoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> DemoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<GroupMessageDto>> SendMessageAsync(Guid groupId, Guid senderId, string text, CancellationToken cancellationToken = default);
        Task<ApiResponse<IEnumerable<GroupMessageDto>>> GetMessagesAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetMembersAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
    }
}
