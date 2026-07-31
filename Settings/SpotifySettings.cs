namespace BlogGraphQlApp.Settings
{
    public class SpotifySettings
    {
        public static string SectionName => nameof(SpotifySettings);
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
