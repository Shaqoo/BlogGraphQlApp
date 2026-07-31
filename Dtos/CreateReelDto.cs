﻿namespace BlogGraphQlApp.DTOs
{
    public class CreateReelDto
    {
        public required string Title { get; set; }
        public required IFile Video { get; set; }
        public Guid UserId { get; set; }
    }
}