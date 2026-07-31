﻿namespace BlogGraphQlApp.DTOs
{
    public class ReelDto
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string VideoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto? User { get; set; }
        public int ReactionCount { get; set; }
        public int ReplyCount { get; set; }
        public long Views { get; set; }
        public ICollection<ReplyDto> Replies { get; set; } = [];
        public ICollection<ReactionDto> Reactions { get; set; } = [];
    }
}