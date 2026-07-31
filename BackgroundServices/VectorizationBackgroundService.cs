using BlogGraphQlApp.External;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.BackgroundServices
{
    public class VectorizationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<VectorizationBackgroundService> _logger;
        private readonly TimeSpan _delay = TimeSpan.FromMinutes(2); 
        public VectorizationBackgroundService(IServiceProvider services, ILogger<VectorizationBackgroundService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Vectorization background service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var vectorService = scope.ServiceProvider.GetRequiredService<ContentVectorService>();

                    var posts = await uow.Posts
                        .Find(p => !p.IsVectorized)
                        .ToListAsync(stoppingToken);

                    foreach (var post in posts)
                    {
                        try
                        {
                            await vectorService.UpsertPostAsync(post);
                            post.IsVectorized = true;
                            uow.Posts.Update(post);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to vectorize post {PostId}", post.Id);
                        }
                    }

                    // Optionally handle Reels similarly
                    //var reels = await uow.Reels
                    //    .Find(r => !r.IsVectorized)
                    //    .ToListAsync(stoppingToken);

                    //foreach (var reel in reels)
                    //{
                    //    try
                    //    {
                    //        await vectorService.u(reel);
                    //        reel.IsVectorized = true;
                    //        db.Reels.Update(reel);
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        _logger.LogError(ex, "Failed to vectorize reel {ReelId}", reel.Id);
                    //    }
                    //}

                    await uow.CompleteAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in vectorization background service loop.");
                }

                await Task.Delay(_delay, stoppingToken);
            }

            _logger.LogInformation("Vectorization background service stopped.");
        }
    }
}
