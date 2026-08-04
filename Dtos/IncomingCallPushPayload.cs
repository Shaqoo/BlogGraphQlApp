using System.Text.Json.Serialization;

namespace BlogGraphQlApp.DTOs
{
    public class IncomingCallPushPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "video_call";

        [JsonPropertyName("callId")]
        public Guid CallId { get; set; }

        [JsonPropertyName("roomName")]
        public string RoomName { get; set; } = string.Empty;

        [JsonPropertyName("callerId")]
        public Guid CallerId { get; set; }

        [JsonPropertyName("callerName")]
        public string CallerName { get; set; } = string.Empty;

        [JsonPropertyName("callerAvatar")]
        public string? CallerAvatar { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = "/call/";
    }
}
