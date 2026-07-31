﻿using System.ComponentModel.DataAnnotations;

namespace BlogGraphQlApp.DTOs
{
    public class UpdateUserUsernameDto
    {
        [Required(ErrorMessage = "Username is required.")]
        [RegularExpression("^[a-zA-Z_]+$", ErrorMessage = "Username can only contain letters and underscores.")]
        public required string Username { get; set; }
    }
}