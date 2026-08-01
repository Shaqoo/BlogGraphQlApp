using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupCallTypeGql : ObjectType<GroupCallDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupCallDto> descriptor)
        {
            descriptor.Description("A group video call powered by Daily.co.");
            descriptor.Field(f => f.Token)
                .Description("One-time Daily meeting token, returned when joining a call.");
        }
    }
}
