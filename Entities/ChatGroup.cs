using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class ChatGroup : BaseEntity
    {
        public required string Name { get; set; }
        public Guid CreatedBy { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsPrivate { get; set; }
        public string? InviteCode { get; set; }
        public Guid? LastMessageId { get; set; }
        public GroupMessage? LastMessage { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public new DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool Archived { get; set; }
        public int? MaxMembers { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public ICollection<ChatGroupMember> Members { get; set; } = [];
        public ICollection<GroupMessage> Messages { get; set; } = [];
        public ICollection<GroupVideoCall> VideoCalls { get; set; } = [];
        public ICollection<GroupJoinRequest> JoinRequests { get; set; } = [];
    }
}
