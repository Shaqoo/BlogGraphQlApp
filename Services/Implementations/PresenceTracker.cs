using System.Collections.Concurrent;

namespace BlogGraphQlApp.Services.Implementations
{
    public class PresenceTracker
    {
        // Dictionary of userId -> connectionIds
        private static readonly ConcurrentDictionary<Guid, HashSet<string>> OnlineUsers
            = new ConcurrentDictionary<Guid, HashSet<string>>() ;

        private static readonly object _lock = new object();

        public async Task UserConnected(Guid userId, string connectionId)
        {
            lock (_lock)
            {
                if (!OnlineUsers.ContainsKey(userId))
                {
                    OnlineUsers[userId] = new HashSet<string>();
                }
                OnlineUsers[userId].Add(connectionId);
            }
            await Task.CompletedTask;
        }

        public async Task UserDisconnected(Guid userId, string connectionId)
        {
            lock (_lock)
            {
                if (!OnlineUsers.ContainsKey(userId)) return;

                OnlineUsers[userId].Remove(connectionId);
                if (OnlineUsers[userId].Count == 0)
                {
                    OnlineUsers.Remove(userId,out var val);
                }
            }
            await Task.CompletedTask;
        }

        public Task<bool> IsOnline(Guid userId)
        {
            lock (_lock)
            {
                return Task.FromResult(OnlineUsers.ContainsKey(userId));
            }
        }

        public async Task<List<Guid>> GetOnlineUsers()
        {
            await Task.CompletedTask;
            lock (_lock)
            {
                return OnlineUsers.Keys.ToList();
            }
        }
    }

}
