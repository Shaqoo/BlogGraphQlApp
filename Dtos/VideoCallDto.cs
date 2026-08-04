using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class VideoCallDto
    {
        public Guid CallId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string RoomUrl { get; set; } = string.Empty;
        public string? Token { get; set; }
        public Guid CallerId { get; set; }
        public string CallerName { get; set; } = string.Empty;
        public string? CallerAvatar { get; set; }
        public Guid RecipientId { get; set; }
        public CallMediaType MediaType { get; set; } = CallMediaType.Video;
        public VideoCallStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}
