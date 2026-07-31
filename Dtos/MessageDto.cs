using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class MessageDto
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public UserDto? Sender { get; set; } = null!;
        public MessageType MessageType { get; set; }
        public string? Content { get; set; }
        public string? FileUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public MessageDto? ReplyToMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsDeleted { get; set; } = false;
        public IEnumerable<ReactionDto> Reactions { get; set; } = [];
    }
}