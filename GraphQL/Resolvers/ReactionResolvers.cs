using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class ReactionResolvers
    {
#pragma warning disable CC0091
        public async Task<UserDto> GetUserAsync(
#pragma warning restore CC0091
            [Parent] ReactionDto reaction,
            UserByIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var user = await dataLoader.LoadAsync(reaction.UserId, cancellationToken);
            return mapper.Map<UserDto>(user);
        }
    }
}