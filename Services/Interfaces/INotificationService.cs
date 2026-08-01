using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationDto> CreateAsync(Guid userId, NotificationType type, string message, Guid? relatedEntityId = null, int relatedEntityType = 0, string? metadata = null, CancellationToken ct = default);
        Task<ApiResponse<NotificationDto?>> GetNotificationByIdAsync(Guid id);
        Task<ApiResponse<IQueryable<NotificationDto>>> GetNotificationsForUserAsync();
        Task<ApiResponse<bool>> MarkAsReadAsync(Guid id);
    }
}