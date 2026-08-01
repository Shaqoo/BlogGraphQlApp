namespace BlogGraphQlApp.DTOs
{
    public class GroupCallPushPayload
    {
        public string Type { get; set; } = "group_call";
        public Guid CallId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public Guid StartedById { get; set; }
        public string StartedByName { get; set; } = string.Empty;
        public string Url { get; set; } = "/call/";
    }
}
