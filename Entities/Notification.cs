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
        public Guid? RelatedEntityId { get; set; }
        public int RelatedEntityType { get; set; }
        public string? Metadata { get; set; }
    }
}