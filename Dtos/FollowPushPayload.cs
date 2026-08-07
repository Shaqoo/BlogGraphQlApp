using System.Text.Json.Serialization;

namespace BlogGraphQlApp.DTOs
{
    public class FollowPushPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "new_follower";

        [JsonPropertyName("followerId")]
        public Guid FollowerId { get; set; }

        [JsonPropertyName("followerName")]
        public string FollowerName { get; set; } = string.Empty;

        [JsonPropertyName("followerAvatar")]
        public string? FollowerAvatar { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = "/profile/";
    }
}
