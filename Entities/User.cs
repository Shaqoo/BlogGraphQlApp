﻿using BlogGraphQlApp.Entities;

namespace BlogGraphQlApp.Models
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public required string FullName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? CoverPictureUrl { get; set; }
        public string? BackgroundIdentifier { get; set; }
        public bool IsEmailVerified { get; set; } = false;
        public bool IsPhoneNumberVerified { get; set; } = false;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public ICollection<Post> Posts { get; set; } = [];
        public ICollection<Reel> Reels { get; set; } = [];
        public ICollection<Notification> Notifications { get; set; } = [];
        public ICollection<UserInteraction> UserInteractions { get; set; } = [];
        public ICollection<UserFollow> Followers { get; set; } = [];
        public ICollection<UserFollow> Following { get; set; } = [];
        public ICollection<Reaction> Reactions { get; set; } = [];
        public ICollection<Reply> Replies { get; set; } = [];
        public ICollection<Conversation> Conversations { get; set; } = [];
        public ICollection<Message> Messages { get; set; } = [];
        public ICollection<PostMention> Mentions { get; set; } = [];
    }
}