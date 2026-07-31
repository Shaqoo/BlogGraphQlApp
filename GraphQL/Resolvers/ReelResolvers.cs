﻿using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class ReelResolvers
    {
#pragma warning disable CC0091
        public async Task<IEnumerable<ReplyDto>> GetRepliesAsync(
#pragma warning restore CC0091
            [Parent] ReelDto reel,
            RepliesByReelDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var replies = await dataLoader.LoadAsync(reel.Id, cancellationToken);
            return mapper.Map<IEnumerable<ReplyDto>>(replies);
        }

#pragma warning disable CC0091
        public async Task<IEnumerable<ReactionDto>> GetReactionsAsync(
#pragma warning restore CC0091
            [Parent] ReelDto reel,
            ReactionsByReelIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var reactions = await dataLoader.LoadAsync(reel.Id, cancellationToken);
            return mapper.Map<IEnumerable<ReactionDto>>(reactions);
        }

#pragma warning disable CC0091
        public async Task<UserDto> GetUserAsync(
#pragma warning restore CC0091
            [Parent] ReelDto reel,
            UserByIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var user = await dataLoader.LoadAsync(reel.User!.Id, cancellationToken);
            return mapper.Map<UserDto>(user);
        }
    }
}