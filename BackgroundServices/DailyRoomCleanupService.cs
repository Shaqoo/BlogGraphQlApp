using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.History;
using BlogGraphQlApp.Services.Daily;
using BlogGraphQlApp.Services.Push;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.BackgroundServices
{
    /// <summary>
    /// Automatic Daily room lifecycle management. Runs every minute and:
    /// - marks unanswered (Ringing &gt; 60s) 1-to-1 calls as Missed and deletes their rooms;
    /// - deletes rooms of calls that finished more than 30 minutes ago (safety net);
    /// - ends group calls that have been ringing too long, and cleans their rooms/tokens.
    /// </summary>
    public class DailyRoomCleanupService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MissedAfter = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan GroupRingingTimeout = TimeSpan.FromMinutes(5);

        private readonly IServiceProvider _services;
        private readonly ILogger<DailyRoomCleanupService> _logger;

        public DailyRoomCleanupService(IServiceProvider services, ILogger<DailyRoomCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Daily room cleanup service started.");

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanUpAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Daily room cleanup cycle failed.");
                }
            }
        }

        private async Task CleanUpAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var daily = scope.ServiceProvider.GetRequiredService<IDailyCallService>();
            var push = scope.ServiceProvider.GetRequiredService<IWebPushService>();
            var history = scope.ServiceProvider.GetRequiredService<ICallHistoryService>();
            var events = scope.ServiceProvider.GetRequiredService<ITopicEventSender>();

            var now = DateTime.UtcNow;
            var missed = 0;
            var staleVideo = 0;
            var staleGroup = 0;

            // 1) Ringing calls that were never answered -> Missed, room deleted.
            var unanswered = await uow.ActiveVideoCalls
                .Find(c => c.Status == VideoCallStatus.Ringing && c.CreatedAt < now - MissedAfter)
                .ToListAsync(cancellationToken);

            foreach (var call in unanswered)
            {
                await daily.EndRoomAsync(call.RoomName, cancellationToken);

                call.Status = VideoCallStatus.Missed;
                call.EndedAt = now;
                uow.ActiveVideoCalls.Update(call);

                await history.MissDirectAsync(call.CallId, now, cancellationToken);

                await push.SendToUserAsync(call.CallerId, new IncomingCallPushPayload
                {
                    Type = "call_missed",
                    CallId = call.CallId,
                    RoomName = call.RoomName,
                    CallerId = call.CallerId,
                    Url = $"/call/{call.CallId}"
                }, cancellationToken);

                await PublishAsync(events, $"{call.CallerId}_CallMissed", new VideoCallDto
                {
                    CallId = call.CallId,
                    RoomName = call.RoomName,
                    RoomUrl = call.DailyRoomUrl,
                    CallerId = call.CallerId,
                    CallerName = call.Caller?.FullName ?? string.Empty,
                    CallerAvatar = call.Caller?.ProfilePictureUrl,
                    RecipientId = call.RecipientId,
                    Status = VideoCallStatus.Missed,
                    CreatedAt = call.CreatedAt,
                    EndedAt = now
                }, cancellationToken);
                _logger.LogInformation("Call {CallId} marked as missed (no answer).", call.CallId);
                missed++;
            }

            // 2) Safety net: finished calls whose rooms may still exist.
            var staleVideoCalls = await uow.ActiveVideoCalls
                .Find(c =>
                    (c.Status == VideoCallStatus.Ended || c.Status == VideoCallStatus.Rejected || c.Status == VideoCallStatus.Missed) &&
                    (c.EndedAt ?? c.CreatedAt) < now - StaleAfter)
                .ToListAsync(cancellationToken);

            foreach (var call in staleVideoCalls)
            {
                await daily.EndRoomAsync(call.RoomName, cancellationToken);
                await history.EndDirectAsync(call.CallId, now, null, cancellationToken);
                staleVideo++;
            }

            // 3) Group calls: ringing too long -> end; ended too long -> clean room + tokens.
            var staleGroupCalls = await uow.GroupVideoCalls
                .Find(c =>
                    (c.Status == GroupCallStatus.Ringing && c.CreatedAt < now - GroupRingingTimeout) ||
                    (c.Status == GroupCallStatus.Ended && (c.EndedAt ?? c.CreatedAt) < now - StaleAfter))
                .Include(c => c.Group)
                .ToListAsync(cancellationToken);

            foreach (var call in staleGroupCalls)
            {
                await daily.EndRoomAsync(call.RoomName, cancellationToken);

                if (call.Status != GroupCallStatus.Ended)
                {
                    call.Status = GroupCallStatus.Ended;
                    call.EndedAt = now;
                    uow.GroupVideoCalls.Update(call);
                    var dto = new GroupCallDto
                    {
                        CallId = call.CallId,
                        GroupId = call.GroupId,
                        GroupName = call.Group?.Name ?? string.Empty,
                        RoomName = call.RoomName,
                        RoomUrl = call.DailyRoomUrl,
                        StartedBy = call.StartedBy,
                        Status = GroupCallStatus.Ended,
                        CreatedAt = call.CreatedAt,
                        EndedAt = now
                    };
                    await PublishAsync(events, $"{call.CallId}_GroupCallEnded", dto, cancellationToken);
                    await history.EndGroupAsync(call.CallId, now, cancellationToken);
                }

                var participants = await uow.GroupVideoCallParticipants
                    .Find(p => p.CallId == call.Id)
                    .ToListAsync(cancellationToken);
                foreach (var participant in participants)
                {
                    participant.Token = null;
                    participant.LeftAt ??= now;
                    uow.GroupVideoCallParticipants.Update(participant);
                }

                staleGroup++;
            }

            if (missed + staleVideo + staleGroup > 0)
            {
                await uow.CompleteAsync(cancellationToken);
                _logger.LogInformation("Daily room cleanup: {Missed} missed, {StaleVideo} stale video, {StaleGroup} group call rooms processed.",
                    missed, staleVideo, staleGroup);
            }
        }

        private async Task PublishAsync<T>(ITopicEventSender events, string topic, T payload, CancellationToken cancellationToken)
        {
            try
            {
                await events.SendAsync(topic, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish cleanup event to topic {Topic}.", topic);
            }
        }
    }
}
