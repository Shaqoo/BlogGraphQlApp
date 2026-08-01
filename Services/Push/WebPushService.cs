using BlogGraphQlApp.Config;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using WebPush;

namespace BlogGraphQlApp.Services.Push
{
    /// <summary>
    /// Stores browser push subscriptions per user and delivers Web Push notifications
    /// (RFC 8030) using VAPID authentication. A subscription that the browser no longer
    /// accepts (404/410) is removed automatically.
    /// </summary>
    public class WebPushService : IWebPushService
    {
        private const int MaxConcurrentSends = 5;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IOptions<VapidSettings> _options;
        private readonly ILogger<WebPushService> _logger;

        public WebPushService(IUnitOfWork unitOfWork, IOptions<VapidSettings> options, ILogger<WebPushService> logger)
        {
            _unitOfWork = unitOfWork;
            _options = options;
            _logger = logger;
        }

        private VapidSettings Settings => _options.Value;

        public async Task RegisterAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint is required.", nameof(endpoint));

            var existing = await _unitOfWork.WebPushSubscriptions
                .Find(s => s.UserId == userId && s.Endpoint == endpoint)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                await _unitOfWork.WebPushSubscriptions.AddAsync(new UserWebPushSubscription
                {
                    UserId = userId,
                    Endpoint = endpoint,
                    P256dh = p256dh,
                    Auth = auth
                });
            }
            else
            {
                existing.P256dh = p256dh;
                existing.Auth = auth;
                _unitOfWork.WebPushSubscriptions.Update(existing);
            }

            await _unitOfWork.CompleteAsync(cancellationToken);
        }

        public async Task RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default)
        {
            var subscription = await _unitOfWork.WebPushSubscriptions
                .Find(s => s.UserId == userId && s.Endpoint == endpoint)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is not null)
            {
                _unitOfWork.WebPushSubscriptions.Remove(subscription);
                await _unitOfWork.CompleteAsync(cancellationToken);
            }
        }

        public Task SendToUserAsync(Guid userId, object payload, CancellationToken cancellationToken = default)
            => SendToUsersAsync([userId], payload, cancellationToken);

        public async Task SendToUsersAsync(IEnumerable<Guid> userIds, object payload, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured())
            {
                _logger.LogWarning("Web Push is not configured (VAPID keys missing); skipping notification delivery.");
                return;
            }

            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0)
                return;

            var subscriptions = await _unitOfWork.WebPushSubscriptions
                .Find(s => ids.Contains(s.UserId))
                .ToListAsync(cancellationToken);

            if (subscriptions.Count == 0)
                return;

            var body = JsonSerializer.Serialize(payload);
            var vapid = new VapidDetails(Settings.Subject, Settings.PublicKey, Settings.PrivateKey);

            using var semaphore = new SemaphoreSlim(MaxConcurrentSends);
            var tasks = subscriptions.Select(async subscription =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await SendOneAsync(subscription, body, vapid, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task SendOneAsync(
            UserWebPushSubscription subscription,
            string body,
            VapidDetails vapid,
            CancellationToken cancellationToken)
        {
            using var client = new WebPushClient();
            var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);

            try
            {
                await client.SendNotificationAsync(pushSubscription, body, vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                _logger.LogInformation("Push subscription {Endpoint} is no longer valid; removing it.", subscription.Endpoint);
                _unitOfWork.WebPushSubscriptions.Remove(subscription);
                await _unitOfWork.CompleteAsync(cancellationToken);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                _logger.LogWarning("Push payload too large for subscription {Endpoint}; dropping.", subscription.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web Push delivery failed for subscription {Endpoint}.", subscription.Endpoint);
            }
        }

        private bool IsConfigured() =>
            !string.IsNullOrWhiteSpace(Settings.PublicKey) &&
            !string.IsNullOrWhiteSpace(Settings.PrivateKey) &&
            !string.IsNullOrWhiteSpace(Settings.Subject);
    }
}
