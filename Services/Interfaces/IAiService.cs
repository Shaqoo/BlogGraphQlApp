using BlogGraphQlApp.Entities;

namespace BlogGraphQlApp.Services.Interfaces
{
    public interface IAiService
    {
        Task<List<string>> CaptionAsync(Guid userId, Guid postId);
        Task<string?> ChatAsync(Guid userId, string input);
    }
}
