using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.BackgroundServices
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan RevokedRetention = TimeSpan.FromDays(30);

        private readonly IServiceProvider _services;
        private readonly ILogger<RefreshTokenCleanupService> _logger;

        public RefreshTokenCleanupService(IServiceProvider services, ILogger<RefreshTokenCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Refresh token cleanup service started.");

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanUpAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Refresh token cleanup cycle failed.");
                }
            }
        }

        private async Task CleanUpAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = DateTime.UtcNow;
            var stale = await uow.RefreshTokens
                .Find(t => t.ExpiresAtUtc <= now ||
                           (t.RevokedAtUtc != null && t.RevokedAtUtc <= now - RevokedRetention))
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
            {
                return;
            }

            uow.RefreshTokens.RemoveRange(stale);
            await uow.CompleteAsync(cancellationToken);
            _logger.LogInformation("Refresh token cleanup removed {Count} stale tokens.", stale.Count);
        }
    }
}
