using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessage : BaseEntity
    {
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public Guid SenderId { get; set; }
        public User Sender { get; set; } = null!;
        public required string Text { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool Deleted { get; set; }
    }
}
