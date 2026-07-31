namespace BlogGraphQlApp.DTOs
{
    public class CreateUserInteractionDto
    {
        public Guid UserId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? ReelId { get; set; }
        public int TimeSpentInSeconds { get; set; }
        public bool IsFavorite { get; set; }
    }
}