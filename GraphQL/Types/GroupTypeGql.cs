using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupTypeGql : ObjectType<GroupDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupDto> descriptor)
        {
            descriptor.Description("A group chat group.");

            descriptor.Field(g => g.Id).Description("The unique identifier of the group.");
            descriptor.Field(g => g.Name).Description("The group name.");
            descriptor.Field(g => g.Description).Description("Optional group description.");
            descriptor.Field(g => g.ImageUrl).Description("Optional group image URL.");
            descriptor.Field(g => g.IsPrivate).Description("Whether the group is private (join requests required).");
            descriptor.Field(g => g.InviteCode).Description("Invite code; only visible to admins and the owner.");
            descriptor.Field(g => g.LastMessageId).Description("ID of the most recent message.");
            descriptor.Field(g => g.LastMessage).Type<GroupMessageTypeGql>().Description("The most recent message, for the group list.");
            descriptor.Field(g => g.LastSender).Description("Sender of the most recent message.");
            descriptor.Field(g => g.LastActivityAt).Description("When the group last had activity.");
            descriptor.Field(g => g.UpdatedAt).Description("When the group info was last updated.");
            descriptor.Field(g => g.Archived).Description("Whether the group is archived.");
            descriptor.Field(g => g.MaxMembers).Description("Optional member limit.");
            descriptor.Field(g => g.CreatedBy).Description("The user who created the group.");
            descriptor.Field(g => g.MemberCount).Description("Number of members.");
            descriptor.Field(g => g.UnreadCount).Description("Unread message count for the requesting user.");
            descriptor.Field(g => g.IsMember).Description("Whether the requesting user is already a member.");
        }
    }
}
