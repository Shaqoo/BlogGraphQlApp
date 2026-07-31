﻿using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class ReelType : ObjectType<ReelDto>
    {
        protected override void Configure(IObjectTypeDescriptor<ReelDto> descriptor)
        {
            descriptor.Description("Represents a short video (reel) made by a user.");

            descriptor.Field(r => r.Replies)
                .Description("Gets the replies for this reel.")
                .Type<ListType<NonNullType<ReplyType>>>()
                .ResolveWith<ReelResolvers>(r => r.GetRepliesAsync(default!, default!, default!, default!));

            descriptor.Field(r => r.Reactions)
                .Description("Gets the reactions for this reel.")
                .Type<ListType<NonNullType<ReactionType>>>()
                .ResolveWith<ReelResolvers>(r => r.GetReactionsAsync(default!, default!, default!, default!));

            descriptor.Field(r => r.User)
                .Description("The user who created this reel.")
                .Type<NonNullType<UserType>>()
                .ResolveWith<ReelResolvers>(r => r.GetUserAsync(default!, default!, default!, default!));

            base.Configure(descriptor);
        }
    }
}