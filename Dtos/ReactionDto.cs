using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class ReactionDto
    {
        public Guid Id { get; set; }
        public required string Emoji{ get; set; }
        public Guid UserId { get; set; }
        public required UserDto User { get; set; }
    }
}