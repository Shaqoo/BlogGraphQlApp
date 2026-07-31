using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using Microsoft.IdentityModel.Tokens;
using Scrypt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using BlogGraphQlApp.Repositories.Interfaces;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ICacheService _cacheService;
        private readonly ScryptEncoder _encoder;

        public AuthService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IEmailService emailService,
            ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
            _emailService = emailService;
            _cacheService = cacheService;
            _encoder = new ScryptEncoder();
        }

        public async Task<ApiResponse<UserDto?>> GetCurrentUserAsync()
        {
            var userIdString = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return ApiResponse<UserDto?>.Fail("User not authenticated.");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            return ApiResponse<UserDto?>.Success(_mapper.Map<UserDto>(user));
        }

        public async Task<ApiResponse<string>> LoginAsync(string email, string password)
        {
            var user = _unitOfWork.Users.Find(u => u.Email == email);
            var foundUser = await user.FirstOrDefaultAsync();

            if (foundUser == null || !_encoder.Compare(password, foundUser.PasswordHash))
            {
                return ApiResponse<string>.Fail("Invalid credentials.");
            }

            if (!foundUser.IsEmailVerified)
            {
                var code = new Random().Next(100000, 999999).ToString();
                await _cacheService.SetAsync($"VerificationCode_{foundUser.Email}", code, TimeSpan.FromMinutes(10));
                await _emailService.SendVerificationCodeAsync(foundUser.Email,foundUser.FullName ,code);
                return ApiResponse<string>.Fail("Login successful, but your email is not verified. A new verification code has been sent to your email.");
            }

            return ApiResponse<string>.Success(GenerateJwtToken(foundUser), "Login successful.");
        }

        public async Task<ApiResponse<bool>> RequestVerificationCodeAsync(string email)
        {
            var user = await _unitOfWork.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null) return ApiResponse<bool>.Fail("User with this email not found.");
            if (user.IsEmailVerified) return ApiResponse<bool>.Fail("Email is already verified.");

            var code = new Random().Next(100000, 999999).ToString();
            await _cacheService.SetAsync($"VerificationCode_{email}", code, TimeSpan.FromMinutes(10));
            await _emailService.SendVerificationCodeAsync(email,user.FullName ,code);

            return ApiResponse<bool>.Success(true, "A new verification code has been sent to your email.");
        }

        public async Task<ApiResponse<string>> VerifyEmailAsync(string email, string code)
        {
            var cacheKey = $"VerificationCode_{email}";
            var cachedCode = await _cacheService.GetAsync<string>(cacheKey);

            if (string.IsNullOrEmpty(cachedCode) || cachedCode != code)
            {
                return ApiResponse<string>.Fail("Invalid or expired verification code.");
            }

            var user = await _unitOfWork.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null) return ApiResponse<string>.Fail("User not found.");
            if (user.IsEmailVerified) return ApiResponse<string>.Fail("Email is already verified.");

            user.IsEmailVerified = true;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            await _cacheService.RemoveAsync(cacheKey);
            await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

            return ApiResponse<string>.Success(GenerateJwtToken(user), "Email verified successfully. You are now logged in.");
        }

        public async Task<ApiResponse<bool>> ForgotPasswordAsync(string email)
        {
            var user = await _unitOfWork.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null)
            {
                return ApiResponse<bool>.Success(true, "If an account with this email exists, a password reset token has been sent.");
            }

            
            var token = Guid.NewGuid().ToString();
            var cacheKey = $"PasswordResetToken_{email}";
            await _cacheService.SetAsync(cacheKey, token, TimeSpan.FromMinutes(15));

            await _emailService.SendPasswordResetTokenAsync(email, token);

            return ApiResponse<bool>.Success(true, "If an account with this email exists, a password reset token has been sent.");
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(string email, string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                return ApiResponse<bool>.Fail("Passwords do not match.");
            }

            var cacheKey = $"PasswordResetToken_{email}";
            var cachedToken = await _cacheService.GetAsync<string>(cacheKey);

            if (string.IsNullOrEmpty(cachedToken) || cachedToken != token)
            {
                return ApiResponse<bool>.Fail("Invalid or expired password reset token.");
            }

            var user = await _unitOfWork.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null) return ApiResponse<bool>.Fail("An error occurred."); 
            user.PasswordHash = _encoder.Encode(newPassword);
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            await _cacheService.RemoveAsync(cacheKey);
            return ApiResponse<bool>.Success(true, "Your password has been reset successfully.");
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}