using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    /// <summary>
    /// Permanent per-user participation record of a group call. Independent of the
    /// temporary Daily room and of the live <see cref="GroupVideoCallParticipant"/>.
    /// </summary>
    public class GroupCallParticipantHistory : BaseEntity
    {
        public Guid CallHistoryId { get; set; }
        public CallHistory CallHistory { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime? JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public int DurationSeconds { get; set; }
    }
}
