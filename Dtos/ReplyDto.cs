namespace BlogGraphQlApp.DTOs
{
    public class ReplyDto
    {
        public Guid Id { get; set; }
        public required string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto User { get; set; } = default!;
        public int NestedReplyCount { get; set; }
        public int ReactionCount { get; set; }
        public List<ReactionDto> Reactions { get; set; } = [];
    }
}