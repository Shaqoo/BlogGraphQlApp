namespace BlogGraphQlApp.DTOs
{
    public class GroupMentionDto
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string MentionText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
