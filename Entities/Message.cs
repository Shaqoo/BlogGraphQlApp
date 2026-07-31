using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Models
{
    public class Message : BaseEntity
    {
        public Guid ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;
        public Guid SenderId { get; set; }
        public User Sender { get; set; } = null!;
        public MessageType MessageType { get; set; }
        public string? Content { get; set; }
        public string? FileUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public Message? ReplyToMessage { get; set; }
        public bool IsRead { get; set; } = false;
        public ICollection<Reaction> Reactions { get; set; } = [];
    }
}