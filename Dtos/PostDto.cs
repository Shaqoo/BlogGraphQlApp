﻿using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class PostDto
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public PostType PostType { get; set; }
        public string? Content { get; set; }
        public string? MediaUrl { get; set; }
        public string? BackgroundIdentifier { get; set; }
        public string? AttachedSongTitle { get; set; }
        public string? AttachedSongArtist { get; set; }
        public string? AttachedSongAlbumArtUrl { get; set; }
        public string? AttachedSongPreviewUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto? User { get; set; }
        public Guid UserId { get; set; }
        public int ReactionsCount { get; set; }
        public int RepliesCount { get; set; }
        public long Views { get; set; }
        public long Shares { get; set; }
        public ICollection<ReplyDto> Replies { get; set; } = [];
        public ICollection<ReactionDto> Reactions { get; set; } = [];
    }
}