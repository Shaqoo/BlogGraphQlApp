namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IAgoraService
    {
        string GenerateRtcToken(string channelName, uint uid);
        string GenerateRtmToken(string userId);
    }
}