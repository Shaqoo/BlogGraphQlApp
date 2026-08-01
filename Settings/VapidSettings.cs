namespace BlogGraphQlApp.Config
{
    public class VapidSettings
    {
        public const string SectionName = "WebPush";

        public required string Subject { get; set; }
        public required string PublicKey { get; set; }
        public required string PrivateKey { get; set; }
    }
}
