using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Models
{
    public class Post : BaseEntity
    {
        public required string Title { get; set; }
        public PostType PostType { get; set; }
        public string? Content { get; set; }
        public string? MediaUrl { get; set; }
        public string? BackgroundIdentifier { get; set; }
        public string? AttachedSongTitle { get; set; }
        public string? AttachedSongArtist { get; set; }
        public string? AttachedSongAlbumArtUrl { get; set; }
        public string? AttachedSongPreviewUrl { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public ICollection<Reaction> Reactions { get; set; } = [];
        public ICollection<Reply> Replies { get; set; } = [];
        public long Views { get; set; } = 0;
        public long Shares { get; set; } = 0;
        public ICollection<UserInteraction> UserInteractions { get; set; } = [];
        public ICollection<PostMention> Mentions { get; set; } = [];
        public ICollection<PostHashtag> PostHashtags { get; set; } = [];
        public string? Transcript { get; set; }
        public List<string>? FramePaths { get; set; }
        public bool IsVectorized { get; set; } = false;
    }
}