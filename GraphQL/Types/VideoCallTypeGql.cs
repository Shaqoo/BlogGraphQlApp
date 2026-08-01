using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class VideoCallTypeGql : ObjectType<VideoCallDto>
    {
        protected override void Configure(IObjectTypeDescriptor<VideoCallDto> descriptor)
        {
            descriptor.Description("A 1-to-1 real-time video call powered by Daily.co.");
            descriptor.Field(f => f.Token)
                .Description("One-time Daily meeting token, returned when accepting/joining a call.");
        }
    }
}
