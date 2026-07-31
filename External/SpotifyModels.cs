namespace BlogGraphQlApp.External
{
    public class SpotifyTrack
    {
        public string Name { get; set; } = string.Empty;
        public SpotifyArtist[] Artists { get; set; } = [];
        public SpotifyAlbum Album { get; set; } = default!;
        public string Preview_Url { get; set; } = string.Empty;
    }

    public class SpotifyArtist
    {
        public string Name { get; set; } = string.Empty;
    }

    public class SpotifyAlbum
    {
        public string Name { get; set; } = string.Empty;
        public SpotifyImage[] Images { get; set; } = [];
    }

    public class SpotifyImage
    {
        public string Url { get; set; } = string.Empty;
    }
}
