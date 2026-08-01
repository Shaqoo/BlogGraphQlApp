using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class UserWebPushSubscription : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public required string Endpoint { get; set; }
        public required string P256dh { get; set; }
        public required string Auth { get; set; }
    }
}
