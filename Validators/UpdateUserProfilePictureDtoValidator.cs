using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class UpdateUserProfilePictureDtoValidator : AbstractValidator<UpdateUserProfilePictureDto>
    {
        private const int MaxFileSize = 5 * 1024 * 1024;
        private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml", "image/tiff", "image/avif"];

        public UpdateUserProfilePictureDtoValidator()
        {
            RuleFor(x => x.ProfilePicture)
                .Cascade(CascadeMode.Stop)
                .Must(x => x!.Length <= MaxFileSize).WithMessage($"File size must not exceed {MaxFileSize / 1024 / 1024} MB.")
                .Must(x => AllowedContentTypes.Contains(x!.ContentType)).WithMessage("Invalid file type. Only JPEG, PNG, GIF, WebP, BMP, SVG, TIFF, and AVIF are allowed.")
                .When(x => x.ProfilePicture != null);
            RuleFor(x => x.ProfilePicture).NotNull().WithMessage("A profile picture is required.");
        }
    }
}