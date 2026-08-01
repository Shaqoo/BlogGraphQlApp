using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    /// <summary>
    /// Permanent record of a call (direct or group). Independent of the temporary
    /// Daily room: deleting the room after the call never deletes this record.
    /// </summary>
    public class CallHistory : BaseEntity
    {
        public Guid CallId { get; set; }
        public CallType CallType { get; set; }
        public Guid CallerId { get; set; }
        public User Caller { get; set; } = null!;
        public Guid? RecipientId { get; set; }
        public User? Recipient { get; set; }
        public Guid? GroupId { get; set; }
        public ChatGroup? Group { get; set; }
        public required string RoomName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int DurationSeconds { get; set; }
        public CallHistoryStatus Status { get; set; } = CallHistoryStatus.Ringing;
        public Guid? EndedByUserId { get; set; }
        public User? EndedByUser { get; set; }
        public ICollection<GroupCallParticipantHistory> Participants { get; set; } = [];
    }
}
