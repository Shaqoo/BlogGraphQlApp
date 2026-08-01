using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupMentionTypeGql : ObjectType<GroupMentionDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupMentionDto> descriptor)
            => descriptor.Description("A user mentioned in a group message.");
    }
}
