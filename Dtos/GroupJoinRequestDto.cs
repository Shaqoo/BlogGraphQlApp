using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupJoinRequestDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public JoinRequestStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
