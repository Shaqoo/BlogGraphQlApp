using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupMessageSearchInput
    {
        public string? Text { get; set; }
        public Guid? SenderId { get; set; }
        public Guid? MentionedUserId { get; set; }
        public bool? Pinned { get; set; }
        public MessageType? MediaType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? HasReactions { get; set; }
        public bool? RepliesOnly { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
