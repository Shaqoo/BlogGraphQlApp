namespace BlogGraphQlApp.Models
{
    public class Conversation : BaseEntity
    {
        public ICollection<User> Participants { get; set; } = [];
        public ICollection<Message> Messages { get; set; } = [];
        public Guid LastMessageId { get; set; }
    }
}