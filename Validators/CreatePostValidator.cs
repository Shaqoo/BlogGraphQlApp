namespace BlogGraphQlApp.Validators
{
    using BlogGraphQlApp.DTOs;
    using BlogGraphQlApp.Enums;
    using FluentValidation;

    public class CreatePostValidator : AbstractValidator<CreatePostDto>
    {
        private const long MaxFileSize = 400 * 1024 * 1024;

        public CreatePostValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

            RuleFor(x => x.PostType)
                .IsInEnum().WithMessage("Invalid post type.");


            When(x => x.PostType == PostType.Text, () =>
            {
                RuleFor(x => x.Content)
                    .NotEmpty().WithMessage("Content is required for text posts.");
            });

            When(x => x.PostType == PostType.Image || x.PostType == PostType.Video, () =>
            {
                RuleFor(x => x.MediaUrl)
                    .NotNull().WithMessage("Media file is required for image or video posts.");

                When(x => x.MediaUrl is not null, () =>
                {
                    RuleFor(x => x.MediaUrl!.Length)
                        .LessThanOrEqualTo(MaxFileSize)
                        .WithMessage("Media file must not exceed 300 MB.");

                    RuleFor(x => x.MediaUrl!.ContentType)
                        .Must(ct => ct.StartsWith("video/") || ct.StartsWith("image/"))
                        .WithMessage("Only image or video files are allowed.");

                    RuleFor(x => x.MediaUrl!.ContentType)
                    .Must(ct => new[]
                    {
                        "image/jpeg",
                        "image/png",
                        "image/webp",
                        "image/gif",
                        "video/mp4",
                        "video/mpeg",
                        "video/quicktime",
                        "video/webm" 
                    }.Contains(ct))
                    .WithMessage("Only .jpg, .png, .webp, .gif, .mp4, .mpeg, .mov, or .webm files are allowed.");

                });
            });
        }
    }

}