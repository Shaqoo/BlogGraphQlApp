using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Services.Interfaces;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [Authorize]
    [ExtendObjectType("Query")]
    public class AiQueries
    {
        private readonly IAiService _aiService;

        public AiQueries(IAiService aiService)
        {
            _aiService = aiService;
        }

        // Caption generation
        public async Task<List<string>> GetCaptionsAsync(Guid postId, [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            return await _aiService.CaptionAsync(currentUser.Data!.Id, postId);
        }

        // Chat
        public async Task<string?> ChatAsync(string input, [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            return await _aiService.ChatAsync(currentUser.Data!.Id, input);
        }
    }

}
