﻿using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<string>> LoginAsync(string email, string password);
        Task<ApiResponse<bool>> RequestVerificationCodeAsync(string email);
        Task<ApiResponse<string>> VerifyEmailAsync(string email, string code);
        Task<ApiResponse<bool>> ForgotPasswordAsync(string email);
        Task<ApiResponse<bool>> ResetPasswordAsync(string email, string token, string newPassword, string confirmPassword);
        Task<ApiResponse<UserDto?>> GetCurrentUserAsync();
    }
}