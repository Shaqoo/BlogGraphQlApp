using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class PostResolvers
    {
#pragma warning disable CC0091  
        public async Task<IEnumerable<ReplyDto>> GetRepliesAsync([Parent] PostDto postDto, int limit,
#pragma warning restore CC0091 
        [Service] IMapper mapper, RepliesByPostDataLoader dataLoader, CancellationToken cancellationToken)
        {
            var replies = await dataLoader.LoadAsync(postDto.Id, cancellationToken);
            const int MaxLimit = 20;
            limit = limit > 0 ? Math.Min(limit, MaxLimit) : 3;
            return mapper.Map<IEnumerable<ReplyDto>>(replies!.Take(limit));
        }

#pragma warning disable CC0091  
        public async Task<IEnumerable<ReactionDto>> GetReactionsAsync([Parent] PostDto postDto, int limit,
#pragma warning restore CC0091  
        [Service]IMapper mapper,ReactionsByPostIdDataLoader dataLoader,CancellationToken cancellationToken)
        {
            var reactions = await dataLoader.LoadAsync(postDto.Id, cancellationToken);
            const int MaxLimit = 20;
            limit = limit > 0 ? Math.Min(limit, MaxLimit) : 3;
            return mapper.Map<IEnumerable<ReactionDto>>(reactions!.Take(limit));
        }

#pragma warning disable CC0091
        public async Task<UserDto> GetUserAsync(
#pragma warning restore CC0091
            [Parent] PostDto post,
            UserByIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var user = await dataLoader.LoadAsync(post.UserId, cancellationToken);
            return mapper.Map<UserDto>(user);
        }
    }
}