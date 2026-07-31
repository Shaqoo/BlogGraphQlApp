using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class PostMention : BaseEntity
    {
        public Guid PostId { get; set; }
        public Post Post { get; set; } = default!;

        public Guid MentionedUserId { get; set; }
        public User MentionedUser { get; set; } = default!;
    }

}
