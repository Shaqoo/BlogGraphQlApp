using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class CallHistoryDto
    {
        public Guid Id { get; set; }
        public Guid CallId { get; set; }
        public CallType CallType { get; set; }
        public Guid CallerId { get; set; }
        public string? CallerName { get; set; }
        public string? CallerAvatar { get; set; }
        public Guid? RecipientId { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientAvatar { get; set; }
        public Guid? GroupId { get; set; }
        public string? GroupName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int DurationSeconds { get; set; }
        public CallHistoryStatus Status { get; set; }
        public Guid? EndedByUserId { get; set; }
        public bool IsIncoming { get; set; }
        public IReadOnlyList<CallHistoryParticipantDto> Participants { get; set; } = [];
    }
}
