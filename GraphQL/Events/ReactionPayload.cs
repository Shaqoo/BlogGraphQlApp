namespace BlogGraphQlApp.GraphQL.Events
{
    public class ReactionPayload
    {
        public string Reaction { get; set; } = default!;
        public Guid? PostId { get; set; }
        public Guid? ReelId { get; set; }
        public Guid? MessageId { get; set; }
        public Guid? ConversationId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = default!;
    }

}
