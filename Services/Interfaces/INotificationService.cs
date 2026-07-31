using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface INotificationService
    {
        Task<ApiResponse<NotificationDto?>> GetNotificationByIdAsync(Guid id);
        Task<ApiResponse<IQueryable<NotificationDto>>> GetNotificationsForUserAsync();
        Task<ApiResponse<bool>> MarkAsReadAsync(Guid id);
    }
}