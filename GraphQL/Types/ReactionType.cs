using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class ReactionType : ObjectType<ReactionDto>
    {
        protected override void Configure(IObjectTypeDescriptor<ReactionDto> descriptor)
        {
            descriptor.Description("Represents a reaction (like, love, etc.) to a post or reel.");

            descriptor.Field(r => r.User)
                .Description("The user who created this reaction.")
                .ResolveWith<ReactionResolvers>(r => r.GetUserAsync(default!, default!, default!, default!));
        }
    }
}