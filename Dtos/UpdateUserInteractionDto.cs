namespace BlogGraphQlApp.DTOs
{
    public class UpdateUserInteractionDto
    {
        public int? TimeSpentInSeconds { get; set; }
        public bool? IsFavorite { get; set; }
    }
}