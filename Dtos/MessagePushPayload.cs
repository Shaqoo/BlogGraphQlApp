using System.Text.Json.Serialization;

namespace BlogGraphQlApp.DTOs
{
    public class MessagePushPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "message";

        [JsonPropertyName("conversationId")]
        public Guid ConversationId { get; set; }

        [JsonPropertyName("senderId")]
        public Guid SenderId { get; set; }

        [JsonPropertyName("senderName")]
        public string SenderName { get; set; } = string.Empty;

        [JsonPropertyName("senderAvatar")]
        public string? SenderAvatar { get; set; }

        [JsonPropertyName("preview")]
        public string Preview { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = "/messages/";
    }
}
