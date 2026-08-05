using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string? CreatedByIp { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public Guid? ReplacedByTokenId { get; set; }
    }
}
