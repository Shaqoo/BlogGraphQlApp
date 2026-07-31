namespace BlogGraphQlApp.External
{
    public class TrackDto
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string AlbumArtUrl { get; set; } = string.Empty;
        public string PreviewUrl { get; set; } = string.Empty;
        public string TrackUrl { get; set; } = string.Empty;
    }

    public class SearchResponseDto
    {
        public List<TrackDto> Tracks { get; set; } = new();
    }
}
