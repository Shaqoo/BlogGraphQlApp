using HotChocolate.Types;

namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Validates uploads before they are persisted: size, MIME type and extension,
    /// rejecting dangerous or unsupported file types. Shared by every storage provider
    /// so the same rules apply in Development and Production.
    /// </summary>
    public static class FileValidator
    {
        private static readonly Dictionary<FileCategory, HashSet<string>> AllowedExtensions = new()
        {
            [FileCategory.Image] = new(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" },
            [FileCategory.Video] = new(StringComparer.OrdinalIgnoreCase)
                { ".mp4", ".webm", ".mov", ".m4v", ".mkv" },
            [FileCategory.Audio] = new(StringComparer.OrdinalIgnoreCase)
                { ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac" },
            [FileCategory.Document] = new(StringComparer.OrdinalIgnoreCase)
                { ".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv" }
        };

        private static readonly Dictionary<FileCategory, HashSet<string>> AllowedMimeTypes = new()
        {
            [FileCategory.Image] = new(StringComparer.OrdinalIgnoreCase)
                { "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp" },
            [FileCategory.Video] = new(StringComparer.OrdinalIgnoreCase)
                { "video/mp4", "video/webm", "video/quicktime", "video/x-m4v", "video/x-matroska" },
            [FileCategory.Audio] = new(StringComparer.OrdinalIgnoreCase)
                { "audio/mpeg", "audio/wav", "audio/wave", "audio/ogg", "audio/mp4", "audio/aac", "audio/flac", "audio/x-wav" },
            [FileCategory.Document] = new(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf", "text/plain", "text/csv",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.ms-powerpoint",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation"
            }
        };

        /// <summary>
        /// Extensions that can carry executable content and are never accepted,
        /// even if their MIME type looks harmless.
        /// </summary>
        private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".msp",
            ".vbs", ".vbe", ".js", ".mjs", ".ps1", ".psm1", ".sh", ".bash",
            ".html", ".htm", ".svg", ".xml", ".xhtml", ".jar", ".apk", ".wasm", ".py"
        };

        public static void Validate(IFile file, StorageValidationOptions options)
        {
            if (file is null)
                throw new InvalidFileException("No file was provided.");

            Validate(file.Name, file.ContentType, file.Length, options);
        }

        public static void Validate(string fileName, string? contentType, long? sizeBytes, StorageValidationOptions options)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidFileException("The uploaded file has no name.");

            var extension = System.IO.Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || extension.Length < 2)
                throw new InvalidFileException($"The file '{fileName}' has an unrecognized extension.");

            extension = extension.ToLowerInvariant();

            if (BlockedExtensions.Contains(extension))
                throw new InvalidFileException($"The file type '{extension}' is not allowed.");

            var category = Classify(contentType, extension);
            if (category is null)
                throw new InvalidFileException($"The file '{fileName}' is not a supported type.");

            if (!AllowedExtensions[category.Value].Contains(extension))
                throw new InvalidFileException($"The file extension '{extension}' is not allowed for {category.Value} uploads.");

            if (!string.IsNullOrWhiteSpace(contentType) && !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                var mime = contentType.Split(';', 2)[0].Trim();
                if (!AllowedMimeTypes[category.Value].Contains(mime))
                    throw new InvalidFileException($"The file type '{mime}' is not allowed.");
            }

            if (sizeBytes is > 0)
            {
                var maxSize = options.GetMaxSizeBytes(category.Value);
                if (sizeBytes.Value > maxSize)
                    throw new InvalidFileException(
                        $"The file '{fileName}' is too large ({sizeBytes.Value} bytes). " +
                        $"Maximum allowed is {maxSize} bytes for {category.Value} files.");
            }
        }

        /// <summary>
        /// Maps a file name (by extension) to a MIME type. Used when the client did not supply
        /// a content type, e.g. for generated avatars.
        /// </summary>
        public static string GuessContentType(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".m4v" => "video/x-m4v",
                ".mkv" => "video/x-matroska",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                ".aac" => "audio/aac",
                ".flac" => "audio/flac",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/octet-stream"
            };
        }

        private static FileCategory? Classify(string? contentType, string extension)
        {
            if (!string.IsNullOrWhiteSpace(contentType) && !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                var mime = contentType.Split(';', 2)[0].Trim();
                foreach (var (category, mimes) in AllowedMimeTypes)
                {
                    if (mimes.Contains(mime))
                        return category;
                }
            }

            foreach (var (category, extensions) in AllowedExtensions)
            {
                if (extensions.Contains(extension))
                    return category;
            }

            return null;
        }
    }
}
