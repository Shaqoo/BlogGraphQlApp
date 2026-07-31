namespace BlogGraphQlApp.Config
{
    public class AgoraSettings
    {
        public const string SectionName = "Agora";
        public string AppId { get; set; } = string.Empty;
        public string AppCertificate { get; set; } = string.Empty;
        public uint TokenExpirationInSeconds { get; set; } = 3600; // Default: 1 hour
    }
}