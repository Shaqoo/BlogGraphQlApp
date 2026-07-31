using System.ComponentModel.DataAnnotations;

namespace BlogGraphQlApp.DTOs
{
    public class CreateUserDto(string Username, string Email, string FullName, string Password, string ConfirmPassword)
    {
        [Required(ErrorMessage = "Username is required.")]
        [RegularExpression("^[a-zA-Z_]+$", ErrorMessage = "Username can only contain letters and underscores.")]
        public string Username { get; set; } = Username;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "A valid email is required.")]
        public string Email { get; set; } = Email;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; } = Password;

        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = ConfirmPassword;

        [Required(ErrorMessage = "Full name is required.")]
        [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "Full name can only contain letters and spaces.")]
        public string FullName { get; set; } = FullName;
    }
}