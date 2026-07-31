using BlogGraphQlApp.External;
using static BlogGraphQlApp.External.DeezerService;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class MusicQueries
    {
        public async Task<SearchResponseDto> SearchTracks(string query,
            [Service]SpotifyService spotifyService)
        {
            return await spotifyService.SearchTracksAsync(query);
        }

        public async Task<List<DeezerTrackDto>> SearchDeezerTracks(string query,
            [Service] DeezerService deezerService)
        {
            return await deezerService.SearchTracksAsync(query);
        }

        public async Task<List<JamendoTrackDto>> SearchJamendoTracks(string query,
            [Service] JamendoService jamendoService)
        {
            return await jamendoService.SearchTracksAsync(query);
        }
    }
}
