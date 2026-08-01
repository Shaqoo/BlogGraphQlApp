using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessageMention : BaseEntity
    {
        public Guid MessageId { get; set; }
        public GroupMessage Message { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string MentionText { get; set; } = string.Empty;
        public new DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
