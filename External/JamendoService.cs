using System.Text.Json;

namespace BlogGraphQlApp.External
{

    public class JamendoService
    {
        private readonly HttpClient _httpClient = new();
        private readonly string _clientId = "52231f35";

        public async Task<List<JamendoTrackDto>> SearchTracksAsync(string query, int limit = 10)
        {
            var url = $"https://api.jamendo.com/v3.0/tracks/?client_id={_clientId}&format=json&limit={limit}&search={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonSerializer.Deserialize<JsonElement>(response);

            var tracks = new List<JamendoTrackDto>();
            foreach (var item in json.GetProperty("results").EnumerateArray())
            {
                tracks.Add(new JamendoTrackDto
                {
                    Id = item.GetProperty("id").GetString()!,
                    Title = item.GetProperty("name").GetString()!,
                    Duration = item.GetProperty("duration").GetInt32(),
                    ArtistName = item.GetProperty("artist_name").GetString()!,
                    AlbumName = item.GetProperty("album_name").GetString()!,
                    AlbumImage = item.GetProperty("album_image").GetString()!,
                    AudioUrl = item.GetProperty("audio").GetString()!,
                    DownloadUrl = item.GetProperty("audiodownload").GetString()!,
                    ShareUrl = item.GetProperty("shareurl").GetString()!,
                    LicenseUrl = item.GetProperty("license_ccurl").GetString()!
                });
            }
            return tracks;
        }
        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class JamendoTrackDto
    {
        public string AlbumImage { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AudioUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public int Duration { get; set; }  
        public string Id { get; set; } = string.Empty;
        public string LicenseUrl { get; set; } = string.Empty;
        public string ShareUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

}
