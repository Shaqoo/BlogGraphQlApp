namespace BlogGraphQlApp.Models
{
    public class UserInteraction : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public Guid? PostId { get; set; }
        public Post? Post { get; set; }
        public Guid? ReelId { get; set; }
        public Reel? Reel { get; set; }
        public int TimeSpentInSeconds { get; set; }
        public bool IsFavorite { get; set; } = false;
        public float DecayRate { get; set; } = 0.05f;
    }
}