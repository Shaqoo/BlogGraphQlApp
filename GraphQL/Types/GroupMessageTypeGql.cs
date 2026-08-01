using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupMessageTypeGql : ObjectType<GroupMessageDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupMessageDto> descriptor)
        {
            descriptor.Description("A message sent inside a group chat.");

            descriptor.Field(m => m.Id).Description("The unique identifier of the message.");
            descriptor.Field(m => m.GroupId).Description("The group the message belongs to.");
            descriptor.Field(m => m.SenderId).Description("The user who sent the message.");
            descriptor.Field(m => m.MessageType).Description("The type of the message (text, image, video, document, audio, system).");
            descriptor.Field(m => m.Content).Description("The textual content of the message, if any.");
            descriptor.Field(m => m.FileUrl).Description("The URL of an uploaded file attachment, if any.");
            descriptor.Field(m => m.ReplyToMessageId).Description("The ID of the message this message replies to, if any.");
            descriptor.Field(m => m.CreatedAt).Description("When the message was created.");
            descriptor.Field(m => m.EditedAt).Description("When the message was last edited, if ever.");
            descriptor.Field(m => m.EditedBy).Description("Who edited the message, if ever.");
            descriptor.Field(m => m.Deleted).Description("Whether the message was soft-deleted.");
            descriptor.Field(m => m.IsPinned).Description("Whether the message is pinned.");
            descriptor.Field(m => m.PinnedAt).Description("When the message was pinned.");
            descriptor.Field(m => m.PinnedBy).Description("Who pinned the message.");
            descriptor.Field(m => m.Status).Description("Delivery status of the message.");
            descriptor.Field(m => m.DeliveredCount).Description("Number of members who have received the message.");
            descriptor.Field(m => m.ReadCount).Description("Number of members who have read the message.");
            descriptor.Field(m => m.UnreadCount).Description("Number of members who have not read the message.");

            descriptor.Field(m => m.ReplyToMessage)
                .Type<GroupMessageTypeGql>()
                .ResolveWith<GroupMessageResolvers>(r => r.GetReplyToMessage(default!, default!, default!, default!));

            descriptor.Field(m => m.Mentions)
                .Description("Users mentioned in this message.")
                .ResolveWith<GroupMessageResolvers>(r => r.GetMentions(default!, default!, default!, default!));

            descriptor.Field(m => m.Reactions)
                .Description("Reactions on this message.")
                .ResolveWith<GroupMessageResolvers>(r => r.GetReactions(default!, default!, default!, default!));
        }
    }
}
