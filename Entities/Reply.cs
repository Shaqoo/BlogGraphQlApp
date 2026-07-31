namespace BlogGraphQlApp.Models
{
    public class Reply : BaseEntity
    {
        public required string Content { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public Guid? PostId { get; set; }
        public Post? Post { get; set; }
        public Guid? ReelId { get; set; }
        public Reel? Reel { get; set; }
        public Guid? ParentReplyId { get; set; }
        public Reply? ParentReply { get; set; }
        public ICollection<Reply> NestedReplies { get; set; } = [];
        public ICollection<Reaction> Reactions { get; set; } = [];
        public int ReactionCount { get; set; }
        public int NestedReplyCount { get; set; }
    }
}