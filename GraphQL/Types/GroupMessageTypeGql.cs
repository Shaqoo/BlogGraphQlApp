using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupMessageTypeGql : ObjectType<GroupMessageDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupMessageDto> descriptor)
            => descriptor.Description("A message sent inside a group chat.");
    }
}
