using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class MessagingQueries
    {
        [Authorize]
        [UsePaging(typeof(ConversationTypeGql))]
        [GraphQLDescription("Gets the conversations for the current user.")]
        public async Task<IQueryable<ConversationDto>> GetConversations(
            [Service] IMessagingService messagingService)
        {
            var response = await messagingService.GetConversationsAsync();
            return response.Data!;
        }

        [Authorize]
        [UsePaging(typeof(MessageTypeGql))]
        [GraphQLDescription("Gets the messages for a specific conversation.")]
        public async Task<IQueryable<MessageDto>> GetMessages(
            Guid conversationId,
            [Service] IMessagingService messagingService)
        {
            var response = await messagingService.GetMessagesAsync(conversationId);
            return response.Data!;
        }
    }
}