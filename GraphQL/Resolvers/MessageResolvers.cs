using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class MessageResolvers
    {
        public async Task<IEnumerable<ReactionDto>> GetReactions([Parent] MessageDto message,ReactionsByMessageIdDataLoader reactionsByMessageIdDataLoader
            ,[Service]IMapper mapper,CancellationToken cancellationToken)
        {
            var reactions = await reactionsByMessageIdDataLoader.LoadAsync(message.Id, cancellationToken);
            var reactionDtos = mapper.Map<IEnumerable<ReactionDto>>(reactions);
            return reactionDtos;
        }

        public async Task<MessageDto?> GetReplyToMessage(
        [Parent] MessageDto message,
        IMessagingService service)
        {
            if (message.ReplyToMessageId == null)
                return null;
            var replyMessage = await service.GetMessageByIdAsync(message.ReplyToMessageId.Value);
            return replyMessage.Data;
        }
    }
}
