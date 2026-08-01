namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Configurable upload validation limits. Bound from the <c>Storage:Validation</c>
    /// configuration section (or the <c>Storage__Validation__*</c> environment variables).
    /// </summary>
    public class StorageValidationOptions
    {
        public const string SectionName = "Storage:Validation";

        /// <summary>Hard cap for any upload, regardless of category.</summary>
        public long MaxFileSizeBytes { get; set; } = 200L * 1024 * 1024;

        public long ImageMaxSizeBytes { get; set; } = 10L * 1024 * 1024;

        public long VideoMaxSizeBytes { get; set; } = 200L * 1024 * 1024;

        public long AudioMaxSizeBytes { get; set; } = 100L * 1024 * 1024;

        public long DocumentMaxSizeBytes { get; set; } = 25L * 1024 * 1024;

        /// <summary>Returns the effective size limit for a category, falling back to the global cap.</summary>
        public long GetMaxSizeBytes(FileCategory category) => category switch
        {
            FileCategory.Image when ImageMaxSizeBytes > 0 => ImageMaxSizeBytes,
            FileCategory.Video when VideoMaxSizeBytes > 0 => VideoMaxSizeBytes,
            FileCategory.Audio when AudioMaxSizeBytes > 0 => AudioMaxSizeBytes,
            FileCategory.Document when DocumentMaxSizeBytes > 0 => DocumentMaxSizeBytes,
            _ => MaxFileSizeBytes > 0 ? MaxFileSizeBytes : long.MaxValue
        };
    }
}
