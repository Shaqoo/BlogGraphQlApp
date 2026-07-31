using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class Hashtag : BaseEntity
    {
        // Store without the '#' symbol, e.g. "AI"
        public string Tag { get; set; } = string.Empty;
        // Navigation property
        public ICollection<PostHashtag> PostHashtags { get; set; } = new List<PostHashtag>();
    }
}

