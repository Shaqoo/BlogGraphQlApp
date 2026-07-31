using BlogGraphQlApp.Config;
using BlogGraphQlApp.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class AgoraService : IAgoraService
    {
        private readonly AgoraSettings _agoraSettings;

        public AgoraService(IOptions<AgoraSettings> agoraSettings)
        {
            _agoraSettings = agoraSettings.Value;
        }

        public string GenerateRtcToken(string channelName, uint uid)
        {
            // // Use the correct TokenBuilder for RTC tokens.
            // var tokenBuilder = new RtmTokenBuilder();
            // return tokenBuilder.BuildToken(_agoraSettings.AppId, _agoraSettings.AppCertificate, channelName, uid,
            //    // TokenBuilder.Role.Role_Publisher, _agoraSettings.TokenExpirationInSeconds,
            //     _agoraSettings.TokenExpirationInSeconds);
            return "";
        }

        public string GenerateRtmToken(string userId)
        {
            // var rtmTokenBuilder = new RtmTokenBuilder();
            // return rtmTokenBuilder.BuildToken(
            //     _agoraSettings.AppId,
            //     _agoraSettings.AppCertificate,
            //     userId,
            //     _agoraSettings.TokenExpirationInSeconds,
            //     _agoraSettings.TokenExpirationInSeconds
            // );
            return "";
        }
    }
}