using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class Notification : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public NotificationType NotificationType { get; set; }
        public required string Message { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}