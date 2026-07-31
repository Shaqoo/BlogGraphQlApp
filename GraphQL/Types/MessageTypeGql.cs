using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class MessageTypeGql : ObjectType<MessageDto>
    {
        protected override void Configure(IObjectTypeDescriptor<MessageDto> descriptor)
        {
            descriptor.Description("Represents a single message within a conversation.");

            descriptor.Field(m => m.Id).Description("The unique identifier of the message.");

            descriptor.Field(m => m.ConversationId).Description("The identifier of the conversation this message belongs to.");

            descriptor.Field(a => a.Sender).Description("The user who sent the message.")
                .Type<UserType>();
            
            descriptor.Field(m => m.MessageType).Description("The type of the message (e.g., text, audio).");

            descriptor.Field(m => m.Content).Description("The textual content of the message, if applicable.");

            descriptor.Field(m => m.ReplyToMessageId).Description("The ID of the message this message is replying to, if any.");

            descriptor.Field(m => m.CreatedAt).Description("The timestamp when the message was created.");

            descriptor.Field(m => m.IsRead).Description("Indicates whether the message has been read by the recipient.");

            descriptor.Field(m => m.IsDeleted).Description("Indicates whether the message has been deleted.");

            descriptor
                .Field(a => a.Reactions)
                .Description("The reactions associated with this message.")
                .ResolveWith<MessageResolvers>(a => a.GetReactions(default!, default!, default!, default!));

            descriptor.Field(a => a.ReplyToMessage)
                .Type<MessageTypeGql>()
                .ResolveWith<MessageResolvers>(r => r.GetReplyToMessage(default!, default!));

        }
    }
}