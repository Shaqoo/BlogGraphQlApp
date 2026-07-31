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
    }
}