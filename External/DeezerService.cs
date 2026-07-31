namespace BlogGraphQlApp.External
{
    using System.Net.Http;
    using System.Text.Json;

    public class DeezerService : IDisposable
    {
        private readonly HttpClient _httpClient = new();

        public async Task<List<DeezerTrackDto>> SearchTracksAsync(string query, int limit = 10)
        {
            var response = await _httpClient.GetStringAsync($"https://api.deezer.com/search?q={Uri.EscapeDataString(query)}&limit={limit}");
            var json = JsonSerializer.Deserialize<JsonElement>(response);
            _httpClient.Dispose();

            var tracks = new List<DeezerTrackDto>();
            foreach (var item in json.GetProperty("data").EnumerateArray())
            {
                tracks.Add(new DeezerTrackDto
                {
                    Title = item.GetProperty("title").GetString()!,
                    Artist = item.GetProperty("artist").GetProperty("name").GetString()!,
                    AlbumArtUrl = item.GetProperty("album").GetProperty("cover_medium").GetString()!,
                    PreviewUrl = item.GetProperty("preview").GetString()!,
                    DeezerUrl = item.GetProperty("link").GetString()!
                });
            }
            return tracks;
        }

        public async Task<DeezerTrackDto> GetTrackAsync(long trackId)
        {
            var response = await _httpClient.GetStringAsync($"https://api.deezer.com/track/{trackId}");
            var json = JsonSerializer.Deserialize<JsonElement>(response);

            return new DeezerTrackDto
            {
                Title = json.GetProperty("title").GetString()!,
                Artist = json.GetProperty("artist").GetProperty("name").GetString()!,
                AlbumArtUrl = json.GetProperty("album").GetProperty("cover_medium").GetString()!,
                PreviewUrl = json.GetProperty("preview").GetString()!,
                DeezerUrl = json.GetProperty("link").GetString()!
            };
        }

        public class DeezerTrackDto
        {
            public string Title { get; set; } = string.Empty;
            public string Artist { get; set; } = string.Empty;
            public string AlbumArtUrl { get; set; } = string.Empty;
            public string PreviewUrl { get; set; } = string.Empty;
            public string DeezerUrl { get; set; } = string.Empty;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
