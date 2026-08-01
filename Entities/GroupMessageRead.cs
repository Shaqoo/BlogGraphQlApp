using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessageRead : BaseEntity
    {
        public Guid MessageId { get; set; }
        public GroupMessage Message { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
