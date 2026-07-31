namespace BlogGraphQlApp.GraphQL.Events
{
    public record ReadMessageEvent(Guid conversationId,Guid readByUserId, DateTime Timestamp, Guid? messageId);

}
