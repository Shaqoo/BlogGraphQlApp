namespace BlogGraphQlApp.Services.Push
{
    public interface IWebPushService
    {
        Task RegisterAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken = default);
        Task RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default);
        Task SendToUserAsync(Guid userId, object payload, CancellationToken cancellationToken = default);
        Task SendToUsersAsync(IEnumerable<Guid> userIds, object payload, CancellationToken cancellationToken = default);
    }
}
