using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class ChatGroup : BaseEntity
    {
        public required string Name { get; set; }
        public Guid CreatedBy { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public ICollection<ChatGroupMember> Members { get; set; } = [];
        public ICollection<GroupMessage> Messages { get; set; } = [];
        public ICollection<GroupVideoCall> VideoCalls { get; set; } = [];
    }
}
