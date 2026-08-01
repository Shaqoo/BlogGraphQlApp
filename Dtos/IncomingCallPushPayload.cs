namespace BlogGraphQlApp.DTOs
{
    public class IncomingCallPushPayload
    {
        public string Type { get; set; } = "video_call";
        public Guid CallId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public Guid CallerId { get; set; }
        public string CallerName { get; set; } = string.Empty;
        public string? CallerAvatar { get; set; }
        public string Url { get; set; } = "/call/";
    }
}
