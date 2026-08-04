using System.Text.Json.Serialization;

namespace BlogGraphQlApp.DTOs
{
    public class GroupCallPushPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "group_call";

        [JsonPropertyName("callId")]
        public Guid CallId { get; set; }

        [JsonPropertyName("groupId")]
        public Guid GroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("roomName")]
        public string RoomName { get; set; } = string.Empty;

        [JsonPropertyName("startedById")]
        public Guid StartedById { get; set; }

        [JsonPropertyName("startedByName")]
        public string StartedByName { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = "/call/";
    }
}
