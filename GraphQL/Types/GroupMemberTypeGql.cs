using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupMemberTypeGql : ObjectType<GroupMemberDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupMemberDto> descriptor)
            => descriptor.Description("A member of a group chat group.");
    }
}
