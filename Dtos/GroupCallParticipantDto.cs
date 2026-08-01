namespace BlogGraphQlApp.DTOs
{
    public class GroupCallParticipantDto
    {
        public Guid Id { get; set; }
        public Guid CallId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public DateTime? JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public bool IsMuted { get; set; }
        public bool CameraEnabled { get; set; }
        public bool ScreenSharing { get; set; }
        public bool HandRaised { get; set; }
    }
}
