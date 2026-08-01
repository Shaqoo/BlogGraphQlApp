using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupVideoCall : BaseEntity
    {
        public Guid CallId { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public required string RoomName { get; set; }
        public required string DailyRoomUrl { get; set; }
        public Guid StartedBy { get; set; }
        public GroupCallStatus Status { get; set; } = GroupCallStatus.Ringing;
        public DateTime? EndedAt { get; set; }
        public ICollection<GroupVideoCallParticipant> Participants { get; set; } = [];
    }
}
