using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Models;
using Microsoft.IdentityModel.Tokens;
using Scrypt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using BlogGraphQlApp.Repositories.Interfaces;
using System.Security.Cryptography;

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
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IEmailService emailService,
            ICacheService cacheService,
            ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
            _emailService = emailService;
            _cacheService = cacheService;
            _encoder = new ScryptEncoder();
            _logger = logger;
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

        public async Task<ApiResponse<AuthResultDto>> RefreshTokenAsync(string refreshToken)
        {
            var hash = HashToken(refreshToken);
            var token = await _unitOfWork.RefreshTokens.Find(t => t.TokenHash == hash).FirstOrDefaultAsync();

            if (token is null)
            {
                return ApiResponse<AuthResultDto>.Fail("Invalid refresh token.");
            }

            var now = DateTime.UtcNow;

            if (token.RevokedAtUtc is not null)
            {
                await RevokeAllUserTokensAsync(token.UserId, now);
                _logger.LogWarning("Refresh token reuse detected; revoked all sessions for user {UserId}.", token.UserId);
                return ApiResponse<AuthResultDto>.Fail("Refresh token has been revoked.");
            }

            if (token.ExpiresAtUtc <= now)
            {
                _unitOfWork.RefreshTokens.Remove(token);
                await _unitOfWork.CompleteAsync();
                return ApiResponse<AuthResultDto>.Fail("Refresh token has expired.");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(token.UserId);
            if (user is null)
            {
                _unitOfWork.RefreshTokens.Remove(token);
                await _unitOfWork.CompleteAsync();
                return ApiResponse<AuthResultDto>.Fail("User no longer exists.");
            }

            var (accessToken, rawRefresh, expiresIn) = await IssueTokenPairAsync(user, rotatedFrom: token);
            return ApiResponse<AuthResultDto>.Success(
                new AuthResultDto { AccessToken = accessToken, RefreshToken = rawRefresh, ExpiresIn = expiresIn },
                "Token refreshed successfully.");
        }

        public async Task<ApiResponse<bool>> LogoutAsync(string refreshToken)
        {
            var hash = HashToken(refreshToken);
            var token = await _unitOfWork.RefreshTokens.Find(t => t.TokenHash == hash).FirstOrDefaultAsync();

            if (token is null || token.RevokedAtUtc is not null)
            {
                return ApiResponse<bool>.Success(true, "Logged out.");
            }

            token.RevokedAtUtc = DateTime.UtcNow;
            _unitOfWork.RefreshTokens.Update(token);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<bool>.Success(true, "Logged out successfully.");
        }

        public async Task<ApiResponse<AuthResultDto>> LoginAsync(string email, string password)
        {
            var normalizedEmail = email.Trim();
            var user = await _unitOfWork.Users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
            var now = DateTime.UtcNow;

            if (user is not null)
            {
                if (user.LockoutEndUtc is DateTime lockoutEnd && lockoutEnd > now)
                {
                    return ApiResponse<AuthResultDto>.Fail(LockoutMessage);
                }

                if (user.LockoutEndUtc is not null)
                {
                    user.LockoutEndUtc = null;
                    user.FailedLoginAttempts = 0;
                    _unitOfWork.Users.Update(user);
                    await _unitOfWork.CompleteAsync();
                }
            }
            else
            {
                var cachedFailures = await _cacheService.GetAsync<int>($"LoginFailures_{normalizedEmail}");
                if (cachedFailures >= MaxLoginAttempts)
                {
                    return ApiResponse<AuthResultDto>.Fail("Invalid credentials.");
                }
            }

            if (user is null || !_encoder.Compare(password, user.PasswordHash))
            {
                if (user is not null)
                {
                    user.FailedLoginAttempts += 1;
                    if (user.FailedLoginAttempts >= MaxLoginAttempts)
                    {
                        user.LockoutEndUtc = now.AddMinutes(LoginLockoutMinutes);
                        user.FailedLoginAttempts = 0;
                    }
                    _unitOfWork.Users.Update(user);
                    await _unitOfWork.CompleteAsync();
                }
                else
                {
                    var cachedFailures = await _cacheService.GetAsync<int>($"LoginFailures_{normalizedEmail}");
                    await _cacheService.SetAsync($"LoginFailures_{normalizedEmail}", cachedFailures + 1, TimeSpan.FromMinutes(LoginLockoutMinutes));
                }

                return ApiResponse<AuthResultDto>.Fail("Invalid credentials.");
            }

            if (!user.IsEmailVerified)
            {
                var code = new Random().Next(100000, 999999).ToString();
                await _cacheService.SetAsync($"VerificationCode_{user.Email}", code, TimeSpan.FromMinutes(10));
                await _emailService.SendVerificationCodeAsync(user.Email, user.FullName, code);
                return ApiResponse<AuthResultDto>.Fail("Login successful, but your email is not verified. A new verification code has been sent to your email.");
            }

            if (user.FailedLoginAttempts != 0 || user.LockoutEndUtc is not null)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
                _unitOfWork.Users.Update(user);
                await _unitOfWork.CompleteAsync();
            }
            await _cacheService.RemoveAsync($"LoginFailures_{normalizedEmail}");

            var (accessToken, refreshToken, expiresIn) = await IssueTokenPairAsync(user);
            return ApiResponse<AuthResultDto>.Success(
                new AuthResultDto { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn },
                "Login successful.");
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

        public async Task<ApiResponse<AuthResultDto>> VerifyEmailAsync(string email, string code)
        {
            var cacheKey = $"VerificationCode_{email}";
            var cachedCode = await _cacheService.GetAsync<string>(cacheKey);

            if (string.IsNullOrEmpty(cachedCode) || cachedCode != code)
            {
                return ApiResponse<AuthResultDto>.Fail("Invalid or expired verification code.");
            }

            var user = await _unitOfWork.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null) return ApiResponse<AuthResultDto>.Fail("User not found.");
            if (user.IsEmailVerified) return ApiResponse<AuthResultDto>.Fail("Email is already verified.");

            user.IsEmailVerified = true;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            await _cacheService.RemoveAsync(cacheKey);
            await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

            var (accessToken, refreshToken, expiresIn) = await IssueTokenPairAsync(user);
            return ApiResponse<AuthResultDto>.Success(
                new AuthResultDto { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn },
                "Email verified successfully. You are now logged in.");
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
            await RevokeAllUserTokensAsync(user.Id, DateTime.UtcNow);
            return ApiResponse<bool>.Success(true, "Your password has been reset successfully.");
        }

        private (string Token, int ExpiresIn) GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var expires = DateTime.UtcNow.AddMinutes(AccessTokenMinutes);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return (tokenHandler.WriteToken(token), (int)Math.Round((expires - DateTime.UtcNow).TotalSeconds));
        }

        private async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> IssueTokenPairAsync(User user, RefreshToken? rotatedFrom = null)
        {
            var (accessToken, expiresIn) = GenerateAccessToken(user);
            var rawRefresh = GenerateRefreshToken();

            var entity = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(rawRefresh),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays),
                CreatedByIp = GetClientIp()
            };

            if (rotatedFrom is not null)
            {
                rotatedFrom.RevokedAtUtc = DateTime.UtcNow;
                rotatedFrom.ReplacedByTokenId = entity.Id;
                _unitOfWork.RefreshTokens.Update(rotatedFrom);
            }

            await _unitOfWork.RefreshTokens.AddAsync(entity);
            await _unitOfWork.CompleteAsync();

            return (accessToken, rawRefresh, expiresIn);
        }

        private async Task RevokeAllUserTokensAsync(Guid userId, DateTime now)
        {
            var activeTokens = await _unitOfWork.RefreshTokens
                .Find(t => t.UserId == userId && t.RevokedAtUtc == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAtUtc = now;
                _unitOfWork.RefreshTokens.Update(token);
            }

            if (activeTokens.Count > 0)
            {
                await _unitOfWork.CompleteAsync();
            }
        }

        private static string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private string? GetClientIp() =>
            _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        private static string LockoutMessage =>
            "Account is temporarily locked due to too many failed login attempts. Please try again later.";

        private int AccessTokenMinutes => GetIntConfig("Jwt:AccessTokenMinutes", 30);
        private int RefreshTokenDays => GetIntConfig("Jwt:RefreshTokenDays", 30);
        private int MaxLoginAttempts => GetIntConfig("Jwt:MaxLoginAttempts", 5);
        private int LoginLockoutMinutes => GetIntConfig("Jwt:LoginLockoutMinutes", 10);

        private int GetIntConfig(string key, int fallback) =>
            int.TryParse(_configuration[key], out var value) ? value : fallback;
    }
}