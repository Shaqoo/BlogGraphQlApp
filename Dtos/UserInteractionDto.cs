namespace BlogGraphQlApp.DTOs
{
    public class UserInteractionDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? ReelId { get; set; }
        public int TimeSpentInSeconds { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}