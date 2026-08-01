using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupTypeGql : ObjectType<GroupDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupDto> descriptor)
            => descriptor.Description("A group chat group.");
    }
}
