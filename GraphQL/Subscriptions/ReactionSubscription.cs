using BlogGraphQlApp.GraphQL.Events;

namespace BlogGraphQlApp.GraphQL.Subscriptions
{
    [ExtendObjectType("Subscription")]
    public class ReactionSubscription
    {
        [Subscribe]
        public ReactionPayload OnPostReactionAdded(
        Guid postId,
        [EventMessage] ReactionPayload reaction)
        => reaction;

        [Subscribe]
        public ReactionPayload OnMessageReactionAdded(
        Guid conversationId,
        [EventMessage] ReactionPayload reaction)
        => reaction;


        [Subscribe]
        public ReactionPayload OnReelReactionAdded(
        Guid reelId,
        [EventMessage] ReactionPayload reaction)
        => reaction;


    }
}
