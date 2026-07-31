using BlogGraphQlApp.Enums;
using BlogGraphQlApp.GraphQL.Mutations;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class SendMessageInputValidator : AbstractValidator<MessagingMutation.SendMessageInput>
    {
        private static readonly string[] AllowedAudioTypes = ["audio/mpeg", "audio/wav", "audio/aac", "audio/ogg","audio/mp3"];
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml", "image/tiff", "image/avif"];
        private static readonly string[] AllowedDocumentTypes =
        [
            // Standard office documents
            "application/pdf",                                                // PDF
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // DOCX
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",       // XLSX
            "application/vnd.openxmlformats-officedocument.presentationml.presentation", // PPTX

            // Google Workspace cloud documents
            "application/vnd.google-apps.document",      // Google Docs
            "application/vnd.google-apps.spreadsheet",   // Google Sheets
            "application/vnd.google-apps.presentation"   // Google Slides
        ];


        public SendMessageInputValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Content) || x.file != null)
                .WithMessage("Message must have either text content or a file.");

            When(x => x.MessageType == MessageType.Text, () =>
            {
                RuleFor(x => x.Content).NotEmpty().WithMessage("Text message cannot be empty.");
                RuleFor(x => x.file).Null().WithMessage("Text message cannot have a file attachment.");
            });

            When(x => x.file != null, () =>
            {
                When(x => x.MessageType == MessageType.Audio, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedAudioTypes.Contains(ct)).WithMessage($"Invalid audio file type. Allowed types are: {string.Join(", ", AllowedAudioTypes)}");
                });

                When(x => x.MessageType == MessageType.Image, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedImageTypes.Contains(ct)).WithMessage($"Invalid image file type. Allowed types are: {string.Join(", ", AllowedImageTypes)}");
                });

                When(x => x.MessageType == MessageType.Document, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedDocumentTypes.Contains(ct)).WithMessage($"Invalid file type. Allowed types are: {string.Join(", ",AllowedDocumentTypes)}.");
                });
            });
        }
    }
}