using BlogGraphQlApp.Settings;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BlogGraphQlApp.External
{
    public class SpotifyService
    {
        private readonly SpotifySettings _spotifySettings;
        private string? _accessToken;

        public SpotifyService(IOptions<SpotifySettings> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _spotifySettings = options.Value;
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            using var client = new HttpClient();
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_spotifySettings.ClientId}:{_spotifySettings.ClientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var body = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" }
        };

            using (var formUrlEncodedContent = new FormUrlEncodedContent(body))
            {
                var response = await client.PostAsync("https://accounts.spotify.com/api/token", formUrlEncodedContent);
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                _accessToken = result.GetProperty("access_token").GetString();
                client.Dispose();
                return _accessToken;
            }
        }

        public async Task<SearchResponseDto> SearchTracksAsync(string query, int limit = 10)
        {
            if (string.IsNullOrEmpty(_accessToken))
                await GetAccessTokenAsync();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await client.GetAsync($"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit={limit}");
            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<JsonElement>(json);
            var tracks = new List<TrackDto>();

#pragma warning disable CC0021 // Use nameof
            foreach (var item in result.GetProperty("tracks").GetProperty("items").EnumerateArray())
            {
#pragma warning disable CS8601 // Possible null reference assignment.
                tracks.Add(new TrackDto
                {
                    Title = item.GetProperty("name").GetString(),
                    Artist = string.Join(@", ", item.GetProperty("artists").EnumerateArray().Select(a => a.GetProperty("name").GetString())),
                    AlbumArtUrl = item.GetProperty("album").GetProperty("images")[0].GetProperty("url").GetString(),
                    PreviewUrl = item.TryGetProperty("preview_url", out var preview) ? preview.GetString() : null,
                    TrackUrl = item.GetProperty("external_urls").GetProperty("spotify").GetString()
                });
#pragma warning restore CS8601 // Possible null reference assignment.
            }
#pragma warning restore CC0021 // Use nameof

            client.Dispose();

            return new SearchResponseDto { Tracks = tracks };
        }
    }

}
