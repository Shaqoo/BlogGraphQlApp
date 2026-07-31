using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class ModerationResult : BaseEntity
    {
        public Guid PostId { get; set; }
        public Post Post { get; set; } = null!;
        public bool Allowed { get; set; }
        public List<ModerationCategory> Categories { get; set; } = new();
        public string Rationale { get; set; } = string.Empty;
        public PostType MediaType { get; set; } = PostType.Text;
    }

}
