using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupJoinRequest : BaseEntity
    {
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public Guid? ResolvedBy { get; set; }
    }
}
