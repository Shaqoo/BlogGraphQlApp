using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class ConversationTypeGql : ObjectType<ConversationDto>
    {
        protected override void Configure(IObjectTypeDescriptor<ConversationDto> descriptor)
            => descriptor.Description("Represents a conversation between two or more users.");
    }
}