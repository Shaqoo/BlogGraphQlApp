using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class ChatGroupMember : BaseEntity
    {
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
