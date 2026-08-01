using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.GraphQL.Events;
using FluentValidation;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class MessagingMutation
    {
        public record SendMessageInput(Guid ToUserId, MessageType MessageType, string? Content, IFile? file, Guid? ReplyToMessageId);

        [Authorize]
        [GraphQLDescription("Sends a message to another user. Can be text or an audio file.")]
        public async Task<ApiResponse<MessageDto>> SendMessageAsync(
            SendMessageInput input,
            [Service] IMessagingService messagingService,
            [Service] ITopicEventSender eventSender,
            [Service] IValidator<SendMessageInput> validator)
        {
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<MessageDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var response = await messagingService.SendMessageAsync(input.ToUserId,input.MessageType ,input.Content, input.file, input.ReplyToMessageId);

            if (response.Succeeded && response.Data != null)
            {
                // Publish the new message to subscribers of this conversation
                var topic = $"{response.Data.ConversationId}_MessageSent";
                await eventSender.SendAsync(topic, response.Data);
            }

            return response;
        }

        public async Task<TypingEvent> SendTyping(Guid conversationId, Guid userId, string name, bool isTyping, [Service] ITopicEventSender eventSender)
        {
            var typingEvent = new TypingEvent(userId, name, conversationId, isTyping, DateTime.UtcNow);
            var topic = $"{conversationId}_UserTyping";
            await eventSender.SendAsync(topic, typingEvent);
            return typingEvent;
        }

        public async Task<RecordingEvent> SendRecording(Guid conversationId, Guid userId, string name, bool isRecording, [Service] ITopicEventSender eventSender)
        {
            var recordingEvent = new RecordingEvent(userId, name, conversationId, isRecording, DateTime.UtcNow);
            var topic = $"{conversationId}_UserRecording";
            await eventSender.SendAsync(topic, recordingEvent);
            return recordingEvent;
        }

        [Authorize]
        public async Task<ApiResponse<bool>> MarkAsRead(Guid conversationId,
            [Service] IMessagingService messagingService, [Service]IAuthService authService,[Service] ITopicEventSender eventSender)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            var markReadResponse = await messagingService.MarkAllAsReadAsync(conversationId);

            if(!markReadResponse.Succeeded)
            {
                return markReadResponse;
            }
            var readMessageEvent = new ReadMessageEvent(conversationId,currentUser.Data!.Id, DateTime.UtcNow,Guid.Empty);
            var topic = $"{conversationId}_MessageRead";
            await eventSender.SendAsync(topic, readMessageEvent);

            return markReadResponse;
        }

        //public async Task<ApiResponse<bool>> MarkMessageAsReadAsync(
        //    Guid conversationId,
        //    Guid messageId,
        //    [Service] IMessagingService messagingService, [Service]ITopicEventSender topicEventSender)
        //{
        //    var markReadResponse = await messagingService.MarkAsReadAsync(messageId);
        //    if (markReadResponse.Succeeded)
        //    {
        //        var readMessageEvent = new ReadMessageEvent(conversationId, DateTime.UtcNow,messageId);
        //        var topic = $"{conversationId}_MessageRead";
        //        await topicEventSender.SendAsync(topic, readMessageEvent);
        //    }
        //    return markReadResponse;
        //}
    }
}