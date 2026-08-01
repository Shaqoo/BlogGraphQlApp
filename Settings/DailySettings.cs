namespace BlogGraphQlApp.Config
{
    public class DailySettings
    {
        public const string SectionName = "Daily";

        public required string ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.daily.co/v1";
        public string? Subdomain { get; set; }
    }
}
