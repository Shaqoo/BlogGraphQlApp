using BlogGraphQlApp.Config;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BlogGraphQlApp.Services.Daily
{
    /// <summary>
    /// Client for the Daily REST API (https://docs.daily.co/reference/rest-api).
    ///
    /// The Daily API key (secret) is read from configuration only and is used to
    /// authenticate every request. It is never exposed to clients; room creation and
    /// meeting-token generation happen exclusively on the backend.
    /// </summary>
    public class DailyCallService : IDailyCallService
    {
        private const int DefaultRoomDurationMinutes = 30;

        private readonly HttpClient _http;
        private readonly IOptions<DailySettings> _options;
        private readonly ILogger<DailyCallService> _logger;

        public DailyCallService(HttpClient http, IOptions<DailySettings> options, ILogger<DailyCallService> logger)
        {
            _http = http;
            _options = options;
            _logger = logger;
        }

        private DailySettings Settings => _options.Value;

        /// <summary>
        /// Fallback meeting URL: https://{DAILY_SUBDOMAIN}.daily.co/{roomName}.
        /// The Daily API normally returns the full URL; this is only used if it does not.
        /// </summary>
        private string? BuildMeetingUrl(string roomName) =>
            string.IsNullOrWhiteSpace(Settings.Subdomain)
                ? null
                : $"https://{Settings.Subdomain}.daily.co/{roomName}";

        public async Task<DailyRoom> CreateRoomAsync(
            string roomName,
            DateTime expiresAt,
            int maxParticipants,
            CancellationToken cancellationToken = default,
            bool audioOnly = false)
        {
            var payload = new
            {
                name = roomName,
                privacy = "private",
                properties = new
                {
                    exp = ToUnixSeconds(expiresAt),
                    max_participants = maxParticipants,
                    enable_chat = false,
                    enable_screenshare = false,
                    permissions = audioOnly ? new { canSend = new[] { "audio" } } : null
                }
            };

            using var response = await SendAsync(HttpMethod.Post, "/rooms", payload, cancellationToken);
            var json = await ReadJsonAsync(response, cancellationToken);

            var url = json.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(url))
                url = BuildMeetingUrl(roomName);
            var name = json.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Daily room created but response did not include url/name.");
                throw new DailyApiException("Daily returned an invalid room response.");
            }

            _logger.LogInformation("Daily room {RoomName} created, expires at {ExpiresAt}.", roomName, expiresAt);
            return new DailyRoom(name, url);
        }

        public async Task<string> CreateMeetingTokenAsync(
            string roomName,
            string userName,
            bool isOwner,
            DateTime expiresAt,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                properties = new
                {
                    room_name = roomName,
                    user_name = userName,
                    is_owner = isOwner,
                    exp = ToUnixSeconds(expiresAt),
                    enable_screenshare = false
                }
            };

            using var response = await SendAsync(HttpMethod.Post, "/meeting-tokens", payload, cancellationToken);
            var json = await ReadJsonAsync(response, cancellationToken);

            var token = json.TryGetProperty("token", out var tokenProp) ? tokenProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Daily meeting token request did not return a token.");
                throw new DailyApiException("Daily returned an invalid meeting token response.");
            }

            _logger.LogInformation("Daily meeting token issued for {RoomName} (owner={IsOwner}).", roomName, isOwner);
            return token;
        }

        public async Task EndRoomAsync(string roomName, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await SendAsync(HttpMethod.Delete, $"/rooms/{Uri.EscapeDataString(roomName)}", null, cancellationToken);
                _logger.LogInformation("Daily room {RoomName} deleted.", roomName);
            }
            catch (DailyApiException ex) when (ex.StatusCode == 404)
            {
                _logger.LogInformation("Daily room {RoomName} already gone (404).", roomName);
            }
        }

        public async Task<DailyRoomStatus> GetRoomAsync(string roomName, CancellationToken cancellationToken = default)
        {
            using var response = await SendAsync(HttpMethod.Get, $"/rooms/{Uri.EscapeDataString(roomName)}", null, cancellationToken);
            var json = await ReadJsonAsync(response, cancellationToken);

            var url = json.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
            var participantCount = 0;
            if (json.TryGetProperty("participants", out var participants) && participants.ValueKind == JsonValueKind.Array)
                participantCount = participants.GetArrayLength();

            return new DailyRoomStatus(roomName, url ?? string.Empty, participantCount);
        }

        private async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string path,
            object? body,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(Settings.ApiKey))
                throw new DailyApiException("Daily API key is not configured.");

            using var request = new HttpRequestMessage(method, Settings.BaseUrl.TrimEnd('/') + path)
            {
                Content = body is null ? null : JsonContent.Create(body)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var detail = TryGetErrorDetail(errorBody);
                _logger.LogError("Daily API {Method} {Path} failed with status {(int)StatusCode}: {Body}",
                    method, path, (int)response.StatusCode, errorBody);
                throw new DailyApiException(
                    detail is null
                        ? $"{DescribeOperation(method, path)} failed with status {(int)response.StatusCode}."
                        : $"{DescribeOperation(method, path)} failed with status {(int)response.StatusCode}: {detail}",
                    (int)response.StatusCode);
            }

            return response;
        }

        private static string DescribeOperation(HttpMethod method, string path) => path switch
        {
            "/rooms" => "Daily room creation",
            "/meeting-tokens" => "Daily meeting token creation",
            _ when method == HttpMethod.Get && path.StartsWith("/rooms/", StringComparison.Ordinal) => "Daily room lookup",
            _ when method == HttpMethod.Delete && path.StartsWith("/rooms/", StringComparison.Ordinal) => "Daily room deletion",
            _ => $"Daily API {method} {path}"
        };

        private static string? TryGetErrorDetail(string body)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.String)
                {
                    var value = info.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    return error.GetString();
            }
            catch (JsonException)
            {
                // Non-JSON error body; fall back to the generic status message.
            }

            return null;
        }

        private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }

        internal static long ToUnixSeconds(DateTime value) =>
            new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeSeconds();

        internal static int ToMinutes(DateTime expiresAt) =>
            Math.Max(1, (int)Math.Ceiling((expiresAt.ToUniversalTime() - DateTime.UtcNow).TotalMinutes));

        public static DateTime DefaultExpiration() => DateTime.UtcNow.AddMinutes(DefaultRoomDurationMinutes);
    }
}
