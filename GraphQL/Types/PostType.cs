using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class PostType : ObjectType<PostDto>
    {
        protected override void Configure(IObjectTypeDescriptor<PostDto> descriptor)
        {
            descriptor.Description("Represents a post made by a user.");

            descriptor
                .Field(a => a.Replies)
                .Description("Returns the latest replies for this post.")
                .Argument("limit", a => a.Type<IntType>().DefaultValue(3))
                .Type<ListType<NonNullType<ReplyType>>>()
                .ResolveWith<PostResolvers>(x => x.GetRepliesAsync(default!, default!, default!, default!, default));

            descriptor
                .Field(a => a.Reactions)
                .Description("Returns the latest reactions for this post, limited by the 'limit' argument.")
                .Argument("limit", a => a.Type<IntType>().DefaultValue(3))
                .Type<ListType<NonNullType<ReactionType>>>()
                .ResolveWith<PostResolvers>(a => a.GetReactionsAsync(default!, default!, default!, default!, default));

            descriptor.Field(p => p.User)
                .Description("The user who created this post.")
                .Type<NonNullType<UserType>>()
                .ResolveWith<PostResolvers>(r => r.GetUserAsync(default!, default!, default!, default!));

            descriptor
                .Field("hasReacted")
                .Description("Indicates whether the current authenticated user has reacted to this post.")
                .Type<NonNullType<BooleanType>>()
                .Resolve(async context =>
                {
                    var post = context.Parent<PostDto>();

                    if (post is null)
                    {
                        throw new GraphQLException("Post not found in context.");
                    }

                    var reactionService = context.Service<IReactionService>();

                    var result = await reactionService.HasUserReactedToPostAsync(post.Id);

                    if (!result.Succeeded)
                    {
                        throw new GraphQLException(result.Message ?? "Failed to check reaction status.");
                    }

                    return result.Data; 
                });

            descriptor
            .Field("userReaction")
            .Description("Shows the current authenticated user's reaction to this post, or null if the user has not reacted.")
            .Type<StringType>() 
            .Resolve(async context =>
            {
                var post = context.Parent<PostDto>();

                if (post is null)
                {
                    throw new GraphQLException("Post not found in context.");
                }

                var reactionService = context.Service<IReactionService>();
                var result = await reactionService.GetUserReactionToPostAsync(post.Id);

                if (!result.Succeeded)
                {
                    if (result.Message == "User is not authenticated.")
                        throw new GraphQLException("User is not authenticated.");

                    return null;
                }

                return result.Data; 
            });


            base.Configure(descriptor);
        }
    }

}