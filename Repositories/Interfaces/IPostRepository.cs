using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Repositories.Interfaces
{
    public interface IPostRepository
    {
        IQueryable<Post> GetPostsByTagAsync(string tag);
    }
}
