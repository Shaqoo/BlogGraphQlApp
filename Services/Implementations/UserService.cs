using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Scrypt;

namespace BlogGraphQlApp.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserService> _logger;
        private readonly IMapper _mapper;
        private readonly IUploadService _uploadService;
        private readonly ICacheService _cacheService;
        private readonly IEmailService _emailService;
        private readonly ScryptEncoder _encoder;
        private readonly IAvatarGeneratorService _avatarGeneratorService;

        public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger, IMapper mapper, IUploadService uploadService, IEmailService emailService, ICacheService cacheService,IAvatarGeneratorService avatarGeneratorService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
            _uploadService = uploadService;
            _emailService = emailService;
            _cacheService = cacheService;
            _encoder = new ScryptEncoder();
            _avatarGeneratorService = avatarGeneratorService;
        }

        public async Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserDto createUserDto)
        {
            var existingUser = await _unitOfWork.Users.AnyAsync(a =>
                    a.Email == createUserDto.Email || a.Username == createUserDto.Username);

            if (existingUser)
            {
                _logger.LogWarning("User creation failed: Email or Username already exists. Email: {Email}, Username: {Username}",
                    createUserDto.Email, createUserDto.Username);

                return ApiResponse<UserDto>.Fail("A user with the same email or username already exists.");
            }

            _logger.LogInformation("Creating a new user with username {Username}", createUserDto.Username);

            var user = _mapper.Map<User>(createUserDto);
            user.PasswordHash = _encoder.Encode(createUserDto.Password);

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            var initials = GetFirstAndLastInitials(user.FullName);
            var avatar = _avatarGeneratorService.GenerateAvatar(initials);
            var avatarUrl = await _uploadService.UploadAvatarAsync(avatar, "profiles");
            user.ProfilePictureUrl = avatarUrl;
            user.CoverPictureUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=1600&q=80";

            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            var code = new Random().Next(100000, 999999).ToString();
            await _cacheService.SetAsync($"VerificationCode_{user.Email}", code, TimeSpan.FromMinutes(10));
            await _emailService.SendVerificationCodeAsync(user.Email,user.FullName ,code);

            return ApiResponse<UserDto>.Success(_mapper.Map<UserDto>(user), "User created successfully. Please check your email to verify your account.");
        }

        public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id)
        {
            _logger.LogInformation("Deleting user with ID {UserId}", id);
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user is null) return ApiResponse<bool>.Fail("User not found.");

            await _uploadService.DeleteFileAsync(user.ProfilePictureUrl);
            await _uploadService.DeleteFileAsync(user.CoverPictureUrl);

            _unitOfWork.Users.Remove(user);
            await _unitOfWork.CompleteAsync();
            return ApiResponse<bool>.Success(true, "User deleted successfully.");
        }

        public Task<ApiResponse<IEnumerable<UserDto>>> GetAllUsersAsync()
        {
            _logger.LogInformation("Getting all users");
            var users =  _unitOfWork.Users.GetAll();
            return Task.FromResult(ApiResponse<IEnumerable<UserDto>>.Success(_mapper.Map<IEnumerable<UserDto>>(users)));
        }

        public async Task<ApiResponse<UserDto?>> GetUserByIdAsync(Guid id)
        {
            _logger.LogInformation("Getting user by ID {UserId}", id);
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            return user is null 
                ? ApiResponse<UserDto?>.Fail($"User with ID {id} not found.") 
                : ApiResponse<UserDto?>.Success(_mapper.Map<UserDto>(user));
        }

        public async Task<ApiResponse<UserDto>> UpdateUserBioAsync(Guid id, UpdateUserBioDto dto)
        {
            _logger.LogInformation("Updating user bio for ID {UserId}", id);
            var user = await GetUserOrThrow(id);
            _mapper.Map(dto, user);
            await _unitOfWork.CompleteAsync();
            await ClearUserCache(id);
            return ApiResponse<UserDto>.Success(_mapper.Map<UserDto>(user), "User bio updated.");
        }

        public async Task<ApiResponse<UserDto>> UpdateUserUsernameAsync(Guid id, UpdateUserUsernameDto dto)
        {
            var existingUsername = await _unitOfWork.Users.AnyAsync(a => a.Username == dto.Username);
            if (existingUsername)
            {
                _logger.LogWarning("User creation failed: Username '{Username}' already exists.", dto.Username);
                return ApiResponse<UserDto>.Fail("This username is already taken.");
            }

            _logger.LogInformation("Updating user username for ID {UserId}", id);
            var user = await GetUserOrThrow(id);
            _mapper.Map(dto, user);
            await _unitOfWork.CompleteAsync();
            await ClearUserCache(id);
            return ApiResponse<UserDto>.Success(_mapper.Map<UserDto>(user), "Username updated.");
        }

        public async Task<ApiResponse<UserDto>> UpdateUserProfilePictureAsync(Guid id, UpdateUserProfilePictureDto dto)
        {
            _logger.LogInformation("Updating user profile picture for ID {UserId}", id);
            var user = await GetUserOrThrow(id);

            if (dto.ProfilePicture != null)
            {
                await _uploadService.DeleteFileAsync(user.ProfilePictureUrl);

                var newUrl = await _uploadService.UploadFileAsync(dto.ProfilePicture, "profiles");
                user.ProfilePictureUrl = newUrl;
            }
            else
            {
                await _uploadService.DeleteFileAsync(user.ProfilePictureUrl);
                user.ProfilePictureUrl = null;
            }

            await _unitOfWork.CompleteAsync();
            await ClearUserCache(id);
            return ApiResponse<UserDto>.Success(_mapper.Map<UserDto>(user), "Profile picture updated.");
        }

        public async Task<ApiResponse<UserDto>> UpdateUserCoverPhotoAsync(Guid id, UpdateUserCoverUrlDto dto)
        {
            _logger.LogInformation("Updating user cover photo for ID {UserId}", id);
            var user = await GetUserOrThrow(id);

            if (dto.CoverPictureUrl != null)
            {
                await _uploadService.DeleteFileAsync(user.CoverPictureUrl);
                var newUrl = await _uploadService.UploadFileAsync(dto.CoverPictureUrl, "covers");
                user.CoverPictureUrl = newUrl;
            }
            else
            {
                await _uploadService.DeleteFileAsync(user.CoverPictureUrl);
                user.CoverPictureUrl = null;
            }

            await _unitOfWork.CompleteAsync();
            await ClearUserCache(id);
            return ApiResponse<UserDto>.Success(_mapper.Map<UserDto>(user), "Cover photo updated.");
        }

        public async Task<ApiResponse<UserDto>> UpdateUserBackgroundIdentifierAsync(Guid id, UpdateUserBackgroundIdentifierDto dto)
        {
            _logger.LogInformation("Updating user background identifier for ID {UserId}", id);
            var user = await GetUserOrThrow(id);
            _mapper.Map(dto, user);
            await _unitOfWork.CompleteAsync();
            await ClearUserCache(id);
            return ApiResponse<UserDto>.Success(_mapper.Map<UserDto>(user), "Background identifier updated.");
        }

        public async Task<ApiResponse<IQueryable<UserDto>>> SearchUsersAsync(string searchTerm)
        {
            _logger.LogInformation("Searching users for term: {SearchTerm}", searchTerm);
            searchTerm = searchTerm.ToLower();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return ApiResponse<IQueryable<UserDto>>.Success(Enumerable.Empty<UserDto>().AsQueryable(), "Search term cannot be empty.");
            }

            // Use EF.Functions.FreeText for full-text search if configured in the database
            // Otherwise, use .Where(u => u.Username.Contains(searchTerm) || u.Email.Contains(searchTerm) || u.FullName.Contains(searchTerm))
            var usersQuery = _unitOfWork.Users
                .Find(u => u.Username.ToLower().Contains(searchTerm) || u.Email.ToLower().Contains(searchTerm) || u.FullName.ToLower().Contains(searchTerm))
                .OrderBy(u => u.Followers.Count())  
                .Select(u => _mapper.Map<UserDto>(u));

            // If FreeText is not supported or configured, you might fall back to:
            // 

            await Task.CompletedTask; // Placeholder for any async operations if needed
            return ApiResponse<IQueryable<UserDto>>.Success(usersQuery);
        }

        private async Task<User> GetUserOrThrow(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            return user ?? throw new System.Collections.Generic.KeyNotFoundException($"User with ID {id} not found.");
        }

        private async Task ClearUserCache(Guid id)
        {
            await _cacheService.RemoveAsync($"user_{id}");
            await _cacheService.RemoveAsync("all_users");
        }

#pragma warning disable CC0091 // Use static method
        public string GetFirstAndLastInitials(string fullName)
#pragma warning restore CC0091 // Use static method
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpper();

            return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
        }

        public async Task<ApiResponse<bool>> CheckIfEmailExists(string email)
        {
            var exists = await _unitOfWork.Users.AnyAsync(u => u.Email == email);

            if (exists)
            {
                _logger.LogInformation("Email check: {Email} already exists.", email);
                return ApiResponse<bool>.Success(true, "This email is already registered.");
            }

            return ApiResponse<bool>.Success(false, "This email is available.");
        }

        public async Task<ApiResponse<bool>> CheckIfUsernameExists(string username)
        {
            var exists = await _unitOfWork.Users.AnyAsync(u => u.Username == username);

            if (exists)
            {
                _logger.LogInformation("Username check: {Username} already exists.", username);
                return ApiResponse<bool>.Success(true, "This username is already taken.");
            }

            return ApiResponse<bool>.Success(false, "This username is available.");
        }

        public async Task<ApiResponse<UserDto?>> GetUserByUsernameAsync(string username)
        {
            _logger.LogInformation("Getting user by Username {Username}", username);
            var user = await _unitOfWork.Users.Find(a => a.Username == username).FirstAsync();
            return user is null
                ? ApiResponse<UserDto?>.Fail($"User with username {username} not found.")
                : ApiResponse<UserDto?>.Success(_mapper.Map<UserDto>(user));
        }
    }
}