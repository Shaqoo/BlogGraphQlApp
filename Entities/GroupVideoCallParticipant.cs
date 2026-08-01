using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupVideoCallParticipant : BaseEntity
    {
        public Guid CallId { get; set; }
        public GroupVideoCall Call { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string? Token { get; set; }
        public DateTime? JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public bool IsMuted { get; set; }
        public bool CameraEnabled { get; set; }
        public bool ScreenSharing { get; set; }
        public bool HandRaised { get; set; }
    }
}
