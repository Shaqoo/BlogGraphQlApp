using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class CreateReactionDto
    {
        public string Emoji { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? ReelId { get; set; }
        public Guid? MessageId { get; set; }
        public Guid? ReplyId { get; set; }
    }
}