﻿namespace BlogGraphQlApp.Models
{
    public class Reel : BaseEntity
    {
        public required string Title { get; set; }
        public required string VideoUrl { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public ICollection<Reaction> Reactions { get; set; } = [];
        public ICollection<Reply> Replies { get; set; } = [];
        public long Views { get; set; } = 0;
        public long Shares { get; set; } = 0;
        public ICollection<UserInteraction> UserInteractions { get; set; } = [];
    }
}