namespace BlogGraphQlApp.Services.Daily
{
    public interface IDailyCallService
    {
        Task<DailyRoom> CreateRoomAsync(string roomName, DateTime expiresAt, int maxParticipants, CancellationToken cancellationToken = default, bool audioOnly = false);
        Task<string> CreateMeetingTokenAsync(string roomName, string userName, bool isOwner, DateTime expiresAt, CancellationToken cancellationToken = default);
        Task EndRoomAsync(string roomName, CancellationToken cancellationToken = default);
        Task<DailyRoomStatus> GetRoomAsync(string roomName, CancellationToken cancellationToken = default);
    }

    public record DailyRoom(string Name, string Url);

    public record DailyRoomStatus(string Name, string Url, int ParticipantCount);
}
