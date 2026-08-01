using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;
        private readonly ITopicEventSender _eventSender;

        public NotificationService(ILogger<NotificationService> logger, IUnitOfWork unitOfWork, IMapper mapper, IAuthService authService, ITopicEventSender eventSender)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _authService = authService;
            _eventSender = eventSender;
        }

        public async Task<NotificationDto> CreateAsync(
            Guid userId,
            NotificationType type,
            string message,
            Guid? relatedEntityId = null,
            int relatedEntityType = 0,
            string? metadata = null,
            CancellationToken ct = default)
        {
            var notification = new Notification
            {
                UserId = userId,
                NotificationType = type,
                Message = message,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                Metadata = metadata
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.CompleteAsync(ct);

            var dto = _mapper.Map<NotificationDto>(notification);
            try
            {
                await _eventSender.SendAsync($"{userId}_User_NotificationReceived", dto, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish notification event for user {UserId}.", userId);
            }
            return dto;
        }

        public async Task<ApiResponse<NotificationDto?>> GetNotificationByIdAsync(Guid id)
        {
            _logger.LogInformation("Getting notification with ID {NotificationId}", id);
            var notification = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (notification == null)
            {
                return ApiResponse<NotificationDto?>.Fail("Notification not found.");
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            if (!currentUser.Succeeded || currentUser.Data?.Id != notification.UserId)
            {
                return ApiResponse<NotificationDto?>.Fail("You are not authorized to view this notification.");
            }

            return ApiResponse<NotificationDto?>.Success(_mapper.Map<NotificationDto>(notification));
        }

        public async Task<ApiResponse<IQueryable<NotificationDto>>> GetNotificationsForUserAsync()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (!currentUser.Succeeded || currentUser.Data == null)
            {
                return ApiResponse<IQueryable<NotificationDto>>.Fail("User not authenticated.");
            }

            var notificationsQuery = _unitOfWork.Notifications
                .Find(n => n.UserId == currentUser.Data.Id)
                .OrderByDescending(n => n.CreatedAt);

            return ApiResponse<IQueryable<NotificationDto>>.Success(_mapper.ProjectTo<NotificationDto>(notificationsQuery));
        }

        public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid id)
        {
            _logger.LogInformation("Marking notification with ID {NotificationId} as read", id);
            var currentUser = await _authService.GetCurrentUserAsync();
            if (!currentUser.Succeeded || currentUser.Data == null)
            {
                return ApiResponse<bool>.Fail("User not authenticated.");
            }

            var notification = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (notification == null)
            {
                return ApiResponse<bool>.Fail("Notification not found.");
            }

            if (notification.UserId != currentUser.Data.Id)
            {
                return ApiResponse<bool>.Fail("You are not authorized to mark this notification as read.");
            }

            notification.ReadAt = DateTime.UtcNow;
            _unitOfWork.Notifications.Update(notification);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<bool>.Success(true, "Notification marked as read.");
        }
    }
}