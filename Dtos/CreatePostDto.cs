﻿using BlogGraphQlApp.Enums;
using System.ComponentModel.DataAnnotations;

namespace BlogGraphQlApp.DTOs
{
    public record CreatePostDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public required string Title { get; set; }
        public PostType PostType { get; set; }
        public string? Content { get; set; }
        public IFile? MediaUrl { get; set; }
        public string? AttachedSongTitle { get; set; }
        public string? AttachedSongArtist { get; set; }
        public string? AttachedSongAlbumArtUrl { get; set; }
        public string? AttachedSongPreviewUrl { get; set; }
        public string? BackgroundIdentifier { get; set; }
    }
}