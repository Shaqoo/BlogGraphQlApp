﻿using System.ComponentModel.DataAnnotations;

namespace BlogGraphQlApp.DTOs
{
    public class UpdateUserBioDto
    {
        [MaxLength(500, ErrorMessage = "Bio cannot be longer than 500 characters.")]
        public string? Bio { get; set; }
    }
}