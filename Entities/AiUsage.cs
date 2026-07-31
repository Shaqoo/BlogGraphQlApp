using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class AiUsage : BaseEntity
    {
        public Guid UserId { get; set; }
        public int RequestCount { get; set; }
        public int ChatRequests { get; set; }
        public int CaptionRequests { get; set; }
        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    }
}
