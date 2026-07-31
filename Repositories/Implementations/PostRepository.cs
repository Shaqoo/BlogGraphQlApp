using BlogGraphQlApp.Data;
using BlogGraphQlApp.Infrastructure;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Repositories.Implementations
{
    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;
        public PostRepository(IDbContextFactory<AppDbContext> factory)
        {
            _context = factory.CreateDbContext();
        }
        public IQueryable<Post> GetPostsByTagAsync(string tag)
        {
            tag = tag.ToLower();

            return _context.Posts
                .Include(p => p.PostHashtags)
                    .ThenInclude(ph => ph.Hashtag)
                .Where(p => p.PostHashtags.Any(ph => ph.Hashtag.Tag == tag));
        }
    }
}
