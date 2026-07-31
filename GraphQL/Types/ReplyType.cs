using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class ReplyType : ObjectType<ReplyDto>
    {
        protected override void Configure(IObjectTypeDescriptor<ReplyDto> descriptor)
        {
            descriptor.Description("Represents a reply made by a user on a post, reel, or another reply.");

            descriptor.Field(r => r.Id)
                .Description("The unique identifier of the reply.");

            descriptor.Field(r => r.Content)
                .Description("The text content of the reply.");

            descriptor.Field(r => r.CreatedAt)
                .Description("The timestamp when the reply was created.");

            descriptor.Field(r => r.User)
                .Description("The user who created this reply.");

            descriptor
                .Field(a => a.Reactions)
                .Description("The list of reactions associated with this reply.")
                .ResolveWith<ReplyResolvers>(r =>
                    r.GetReactionsAsync(default!, default!, default!, default!)
                );

            descriptor
            .Field("hasReacted")
            .Description("Indicates whether the current authenticated user has reacted to this reply.")
            .ResolveWith<ReplyResolvers>(r =>
                r.HasReactedToReplyAsync(default!, default!)
            );

            descriptor
             .Field("userReaction")
             .Description("The reaction type the current authenticated user has given to this reply, or null if none.")
             .ResolveWith<ReplyResolvers>(r =>
                 r.GetUserReactionAsync(default!, default!)
             );

        }
    }
}
