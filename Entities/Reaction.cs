using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Models
{
    public class Reaction : BaseEntity
    {
        public string Emoji { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // A reaction can be on a Post, Reel, or Reply
        public Guid? PostId { get; set; }
        public Post? Post { get; set; }
        public Guid? ReelId { get; set; }
        public Reel? Reel { get; set; }
        public Message? Message { get; set; }
        public Guid? MessageId { get; set; }
        public Reply? Reply { get; set; }
        public Guid? ReplyId { get; set; }
    }
}