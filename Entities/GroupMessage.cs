using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessage : BaseEntity
    {
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public Guid SenderId { get; set; }
        public User Sender { get; set; } = null!;
        public MessageType MessageType { get; set; } = MessageType.Text;
        public string? Content { get; set; }
        public string? FileUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public GroupMessage? ReplyToMessage { get; set; }
        public DateTime? EditedAt { get; set; }
        public Guid? EditedBy { get; set; }
        public bool Deleted { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public Guid? PinnedBy { get; set; }
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public string? Metadata { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public ICollection<GroupMessageMention> Mentions { get; set; } = [];
        public ICollection<GroupMessageRead> Reads { get; set; } = [];
        public ICollection<Reaction> Reactions { get; set; } = [];
    }
}
