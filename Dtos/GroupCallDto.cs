using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupCallDto
    {
        public Guid CallId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomUrl { get; set; } = string.Empty;
        public string? Token { get; set; }
        public Guid StartedBy { get; set; }
        public string StartedByName { get; set; } = string.Empty;
        public GroupCallStatus Status { get; set; }
        public CallMediaType MediaType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}
