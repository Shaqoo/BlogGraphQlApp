namespace BlogGraphQlApp.DTOs
{
    public class CallHistoryParticipantDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? Avatar { get; set; }
        public DateTime? JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public int DurationSeconds { get; set; }
    }
}
