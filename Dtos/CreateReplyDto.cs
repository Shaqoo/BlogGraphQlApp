namespace BlogGraphQlApp.DTOs
{
    public class CreateReplyDto
    {
        public required string Content { get; set; }
        public Guid UserId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? ReelId { get; set; }
        public Guid? ParentReplyId { get; set; }
    }
}