using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupMessageDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public MessageType MessageType { get; set; }
        public string? Content { get; set; }
        public string? FileUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public GroupMessageDto? ReplyToMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public Guid? EditedBy { get; set; }
        public bool Deleted { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public Guid? PinnedBy { get; set; }
        public MessageStatus Status { get; set; }
        public string? Metadata { get; set; }
        public int DeliveredCount { get; set; }
        public int ReadCount { get; set; }
        public int UnreadCount { get; set; }
        public IEnumerable<GroupMentionDto> Mentions { get; set; } = [];
        public IEnumerable<ReactionDto> Reactions { get; set; } = [];
    }
}
