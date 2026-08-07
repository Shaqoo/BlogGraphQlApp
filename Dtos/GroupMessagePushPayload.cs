using System.Text.Json.Serialization;

namespace BlogGraphQlApp.DTOs
{
    public class GroupMessagePushPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "group_message";

        [JsonPropertyName("groupId")]
        public Guid GroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("senderId")]
        public Guid SenderId { get; set; }

        [JsonPropertyName("senderName")]
        public string SenderName { get; set; } = string.Empty;

        [JsonPropertyName("senderAvatar")]
        public string? SenderAvatar { get; set; }

        [JsonPropertyName("preview")]
        public string Preview { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = "/groups/";
    }
}
