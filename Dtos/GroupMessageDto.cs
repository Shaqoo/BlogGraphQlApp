namespace BlogGraphQlApp.DTOs
{
    public class GroupMessageDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool Deleted { get; set; }
    }
}
