using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Services.Groups
{
    /// <summary>
    /// Pure role-based permission rules for group actions. Kept static so the rules
    /// can be unit tested without any infrastructure. Single source of truth.
    /// </summary>
    public static class GroupPermissions
    {
        public static bool CanUpdateGroup(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanChangeImage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanChangeDescription(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanDeleteGroup(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanAddMember(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanRemoveMember(GroupMemberRole actorRole, GroupMemberRole targetRole) =>
            actorRole == GroupMemberRole.Owner || (actorRole == GroupMemberRole.Admin && targetRole == GroupMemberRole.Member);
        public static bool CanPromoteAdmin(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanDemoteAdmin(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanTransferOwnership(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanManageInvite(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanManageJoinRequests(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanPinMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanStartCall(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
        public static bool CanSendMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
        public static bool CanEditMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
        public static bool CanDeleteMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
    }
}
