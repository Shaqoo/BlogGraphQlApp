namespace BlogGraphQlApp.GraphQL.Events
{
    public record TypingEvent(Guid UserId,string name, Guid ConversationId, bool IsTyping, DateTime Timestamp);
}
