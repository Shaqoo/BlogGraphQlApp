using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public NotificationType NotificationType { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? RelatedEntityId { get; set; }
        public int RelatedEntityType { get; set; }
        public string? Metadata { get; set; }
    }
}