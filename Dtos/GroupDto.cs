namespace BlogGraphQlApp.DTOs
{
    public class GroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsPrivate { get; set; }
        public string? InviteCode { get; set; }
        public Guid? LastMessageId { get; set; }
        public GroupMessageDto? LastMessage { get; set; }
        public UserDto? LastSender { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Archived { get; set; }
        public int? MaxMembers { get; set; }
        public Guid CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int MemberCount { get; set; }
        public int UnreadCount { get; set; }
        public bool IsMember { get; set; }
    }
}
