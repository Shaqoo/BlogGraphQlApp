using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class GroupMessageResolvers
    {
        public async Task<IEnumerable<GroupMentionDto>> GetMentions(
            [Parent] GroupMessageDto message,
            MentionsByGroupMessageIdDataLoader loader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var mentions = await loader.LoadAsync(message.Id, cancellationToken);
            return mapper.Map<IEnumerable<GroupMentionDto>>(mentions);
        }

        public async Task<IEnumerable<ReactionDto>> GetReactions(
            [Parent] GroupMessageDto message,
            ReactionsByGroupMessageIdDataLoader loader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var reactions = await loader.LoadAsync(message.Id, cancellationToken);
            return mapper.Map<IEnumerable<ReactionDto>>(reactions);
        }

        public async Task<GroupMessageDto?> GetReplyToMessage(
            [Parent] GroupMessageDto message,
            GroupMessageByIdDataLoader loader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            if (message.ReplyToMessageId is null)
                return null;

            var replies = await loader.LoadAsync(message.ReplyToMessageId.Value, cancellationToken);
            var reply = replies.FirstOrDefault();
            return reply is null ? null : mapper.Map<GroupMessageDto>(reply);
        }
    }
}
