using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupCallParticipantTypeGql : ObjectType<GroupCallParticipantDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupCallParticipantDto> descriptor)
            => descriptor.Description("A participant of a group call and their live state.");
    }
}
