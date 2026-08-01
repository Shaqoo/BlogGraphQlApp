using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class NotificationTypeGql : ObjectType<NotificationDto>
    {
        protected override void Configure(IObjectTypeDescriptor<NotificationDto> descriptor)
        {
            descriptor.Description("Represents a notification for a user.");

            descriptor.Field(n => n.Id).Type<NonNullType<IdType>>();
            descriptor.Field(n => n.Message).Type<NonNullType<StringType>>();
            descriptor.Field(n => n.NotificationType).Type<NonNullType<EnumType<NotificationType>>>();
            descriptor.Field(n => n.IsRead);
            descriptor.Field(n => n.ReadAt).Type<DateTimeType>();
            descriptor.Field(n => n.CreatedAt).Type<NonNullType<DateTimeType>>();
            descriptor.Field(n => n.RelatedEntityId).Type<IdType>().Description("The entity this notification references, if any.");
            descriptor.Field(n => n.RelatedEntityType).Description("Type discriminator of the related entity.");
            descriptor.Field(n => n.Metadata).Description("Structured JSON metadata for the notification payload.");
        }
    }
}