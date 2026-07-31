namespace BlogGraphQlApp.DTOs
{
    public class UpdateUserDto
    {
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? CoverPictureUrl { get; set; }
    }
}