namespace BlogGraphQlApp.GraphQL.Events
{
    public record RecordingEvent(Guid UserId, string name, Guid ConversationId, bool IsRecording, DateTime Timestamp);
}
