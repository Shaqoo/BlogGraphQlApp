namespace BlogGraphQlApp.GraphQL.Events
{
    public record GroupTypingEvent(Guid UserId, string FullName, Guid GroupId, bool IsTyping, DateTime Timestamp);
}
