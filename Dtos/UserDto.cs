namespace BlogGraphQlApp.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public required string FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? CoverPictureUrl { get; set; }
        public string? BackgroundIdentifier { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public long PostsCount { get; set; }
        public long ReelsCount { get; set; }
        public DateTime? LastSeen { get; set; }
        public IEnumerable<NotificationDto> Notifications { get; set; } = [];
        public IEnumerable<PostDto> Posts { get; set; } = [];
        public IEnumerable<ReelDto> Reels { get; set; } = [];
    }
}