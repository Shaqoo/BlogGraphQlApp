using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class ActiveVideoCall : BaseEntity
    {
        public Guid CallId { get; set; } = Guid.NewGuid();
        public required string RoomName { get; set; }
        public required string DailyRoomUrl { get; set; }
        public Guid CallerId { get; set; }
        public User Caller { get; set; } = null!;
        public Guid RecipientId { get; set; }
        public User Recipient { get; set; } = null!;
        public CallMediaType MediaType { get; set; } = CallMediaType.Video;
        public VideoCallStatus Status { get; set; } = VideoCallStatus.Ringing;
        public DateTime? ConnectedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}
