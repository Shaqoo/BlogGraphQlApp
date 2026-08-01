using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupJoinRequestTypeGql : ObjectType<GroupJoinRequestDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupJoinRequestDto> descriptor)
            => descriptor.Description("A pending join request for a private group.");
    }
}
