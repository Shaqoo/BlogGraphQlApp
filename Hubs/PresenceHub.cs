using BlogGraphQlApp.Data;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Implementations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Hubs
{
    public class PresenceHub : Hub
    {
        private readonly PresenceTracker _tracker;
        private readonly IServiceScopeFactory _scopeFactory;

        public PresenceHub(PresenceTracker tracker, IServiceScopeFactory scopeFactory)
        {
            _tracker = tracker;
            _scopeFactory = scopeFactory;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Guid.Parse(Context.UserIdentifier!);

            await _tracker.UserConnected(userId, Context.ConnectionId);

            // Notify others that this user is online
            await Clients.Others.SendAsync("UserOnline", userId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Guid.Parse(Context.UserIdentifier!);

            await _tracker.UserDisconnected(userId, Context.ConnectionId);

            if (!await _tracker.IsOnline(userId))
            {
                // Notify others that this user is offline
                await Clients.Others.SendAsync("UserOffline", userId);

                // Update LastSeen in DB
                await UpdateLastSeen(userId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task UpdateLastSeen(Guid userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var user = await uow.Users.GetByIdAsync(userId);
            if (user != null)
            {
                user.IsOnline = false;
                user.LastSeen = DateTime.UtcNow;
                await uow.CompleteAsync();
            }
        }

        // Optional: heartbeat method for more accurate tracking
        public async Task Heartbeat()
        {
            var userId = Guid.Parse(Context.UserIdentifier!);
            await UpdateLastSeen(userId);
        }
    }

}
