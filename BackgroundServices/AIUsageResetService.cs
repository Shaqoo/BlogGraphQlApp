namespace BlogGraphQlApp.BackgroundServices
{
    using BlogGraphQlApp.Repositories.Interfaces;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class AIUsageResetService : BackgroundService
    {
        private readonly ILogger<AIUsageResetService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AIUsageResetService(ILogger<AIUsageResetService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AIUsageResetService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                var nextRun = now.Date.AddDays(1);
                var delay = nextRun - now;

                _logger.LogInformation("Next reset scheduled at {NextRun}", nextRun);

                try
                {
                    await Task.Delay(delay, stoppingToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                        var usages = unitOfWork.AiUsages.GetAll().ToList();
                        foreach (var usage in usages)
                        {
                            usage.CaptionRequests = 0;
                            usage.ChatRequests = 0;
                            usage.RequestCount = 0;
                        }

                        await unitOfWork.CompleteAsync();
                        _logger.LogInformation("{count} AIUsage counters were successfully reset",usages.Count());
                    }

                    _logger.LogInformation("AIUsage counters reset successfully at {Time}", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while resetting AIUsage counters.");
                }
            }
        }
    }

}
