using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.GraphQL.Events;
using BlogGraphQlApp.Services.Groups;
using FluentValidation;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupMessageMutations
    {
        public record SendGroupMessageInput(Guid GroupId, MessageType MessageType, string? Content, IFile? file, Guid? ReplyToMessageId);

        [Authorize]
        [GraphQLDescription("Sends a message (text or media) in a group.")]
        public async Task<ApiResponse<GroupMessageDto>> SendGroupMessageAsync(
            SendGroupMessageInput input,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            [Service] ITopicEventSender eventSender,
            [Service] IValidator<SendGroupMessageInput> validator,
            CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(input, cancellationToken);
            if (!validationResult.IsValid)
                return ApiResponse<GroupMessageDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());

            var senderId = claimsPrincipal.GetUserId();
            var response = await messageService.SendAsync(input.GroupId, senderId, input.MessageType, input.Content, input.file, input.ReplyToMessageId, cancellationToken);
            return response;
        }

        [Authorize]
        [GraphQLDescription("Edits a group message (sender only).")]
        public async Task<ApiResponse<GroupMessageDto>> EditGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            string content,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var senderId = claimsPrincipal.GetUserId();
            return await messageService.EditAsync(groupId, messageId, senderId, content, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Soft-deletes a group message (sender only).")]
        public async Task<ApiResponse<bool>> DeleteGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var senderId = claimsPrincipal.GetUserId();
            return await messageService.DeleteAsync(groupId, messageId, senderId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Pins a group message (owner or admin only).")]
        public async Task<ApiResponse<GroupMessageDto>> PinGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await messageService.SetPinnedAsync(groupId, messageId, actorId, true, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Unpins a group message (owner or admin only).")]
        public async Task<ApiResponse<GroupMessageDto>> UnpinGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await messageService.SetPinnedAsync(groupId, messageId, actorId, false, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Adds or toggles a reaction emoji on a group message.")]
        public async Task<ApiResponse<bool>> ReactToGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            string emoji,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.ToggleReactionAsync(groupId, messageId, userId, emoji, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Removes the current user's reaction from a group message.")]
        public async Task<ApiResponse<bool>> RemoveGroupReactionAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.RemoveReactionAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Marks a group message as delivered for the current user.")]
        public async Task<ApiResponse<bool>> MarkGroupMessageDeliveredAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.MarkDeliveredAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Marks a group message as read for the current user.")]
        public async Task<ApiResponse<bool>> MarkGroupMessageReadAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.MarkReadAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Marks all group messages as read for the current user.")]
        public async Task<ApiResponse<bool>> MarkAllGroupMessagesReadAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.MarkAllReadAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Broadcasts a typing indicator to members of a group.")]
        public async Task<GroupTypingEvent> NotifyGroupTypingAsync(
            Guid groupId,
            bool isTyping,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            var typingEvent = new GroupTypingEvent(userId, claimsPrincipal.Identity?.Name ?? string.Empty, groupId, isTyping, DateTime.UtcNow);
            await eventSender.SendAsync($"{groupId}_GroupTyping", typingEvent, cancellationToken);
            return typingEvent;
        }
    }
}
