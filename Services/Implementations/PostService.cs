using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;
using BlogGraphQlApp.Repositories.Interfaces;
using System.Text.RegularExpressions;
using BlogGraphQlApp.Entities;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class PostService : IPostService
    {
        private readonly ILogger<PostService> _logger;
        private readonly IPostRepository _postRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;
        private readonly ICacheService _cacheService;
        private readonly IUploadService _uploadService;

        public PostService(ILogger<PostService> logger, IMapper mapper, IUnitOfWork unitOfWork, IUploadService uploadService, IAuthService authService, ICacheService cacheService,IPostRepository postRepository)
        {
            _logger = logger;
            _mapper = mapper;
            _authService = authService;
            _unitOfWork = unitOfWork;
            _uploadService = uploadService;
            _postRepository = postRepository;
            _cacheService = cacheService;
        }

        public async Task<ApiResponse<PostDto>> CreatePostAsync(CreatePostDto createPostDto)
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                _logger.LogWarning("Attempt to create a post without a logged-in user.");
                return ApiResponse<PostDto>.Fail("User not authenticated.");
            }

            _logger.LogInformation("Creating post of type {PostType} for user {UserId}", createPostDto.PostType, currentUser.Data.Id);

            var post = createPostDto.PostType switch
            {
                PostType.Text => await HandleTextPost(createPostDto, currentUser.Data.Id),
                PostType.Image => await HandleMediaPost(createPostDto, currentUser.Data.Id, isVideo: false),
                PostType.Video => await HandleMediaPost(createPostDto, currentUser.Data.Id, isVideo: true),
                _ => throw new ArgumentException("Invalid post type")
            };

            await _unitOfWork.Posts.AddAsync(post);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Post {PostId} created successfully for user {UserId}", post.Id, currentUser.Data.Id);

            var postDto = _mapper.Map<PostDto>(post);
            await _cacheService.SetAsync($"post_{post.Id}", postDto, TimeSpan.FromMinutes(5));

            if (post.Content != null)
            {
                await SaveMentionsAsync(post.Content, post.UserId, post.Id, currentUser.Data.Username);
                await SaveHashtagsAsync(post.Content, post.Id);
            }

            return ApiResponse<PostDto>.Success(postDto);
        }

        public async Task<ApiResponse<PostDto?>> GetPostByIdAsync(Guid id, Guid? currentUserId = null)
        {
            _logger.LogInformation("Getting post with ID {PostId}", id);

            var cacheKey = $"post_{id}";
            var cachedPost = await _cacheService.GetAsync<PostDto>(cacheKey);
            if (cachedPost != null)
            {
                _logger.LogInformation("Returning post {PostId} from cache.", id);
                return ApiResponse<PostDto?>.Success(cachedPost);
            }

            var post = await _unitOfWork.Posts
                .Find(p => p.Id == id)
                .FirstOrDefaultAsync();

            if (post is null)
            {
                _logger.LogWarning("Post with ID {PostId} not found.", id);
                return ApiResponse<PostDto?>.Fail($"Post with ID {id} not found.");
            }

            var postDto = _mapper.Map<PostDto>(post);
            postDto.ReactionsCount = await _unitOfWork.Reactions.CountAsync(r => r.PostId == id);
            postDto.RepliesCount = await _unitOfWork.Replies.CountAsync(r => r.PostId == id);

            await _cacheService.SetAsync(cacheKey, postDto, TimeSpan.FromMinutes(5));

            return ApiResponse<PostDto?>.Success(postDto);
        }

        public async Task<IQueryable<Post>> GetPostsByTagAsync(string tag)
        {
            await Task.CompletedTask;

            return _postRepository.GetPostsByTagAsync(tag);
        }
        public async Task<ApiResponse<PostDto>> UpdatePostAsync(Guid id, UpdatePostDto updatePostDto)
        {
            _logger.LogInformation("Updating post with ID {PostId}", id);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                _logger.LogWarning("Attempt to update post {PostId} without a logged-in user.", id);
                return ApiResponse<PostDto>.Fail("User not authenticated.");
            }

            var post = await _unitOfWork.Posts.GetByIdAsync(id);
            if (post is null)
            {
                _logger.LogWarning("Post with ID {PostId} not found for update.", id);
                return ApiResponse<PostDto>.Fail($"Post with ID {id} not found.");
            }

            if (post.UserId != currentUser.Data.Id)
            {
                _logger.LogWarning("User {UserId} attempted to update post {PostId} which they do not own.", currentUser.Data.Id, id);
                return ApiResponse<PostDto>.Fail("You are not authorized to update this post.");
            }

            _mapper.Map(updatePostDto, post);
            _unitOfWork.Posts.Update(post);
            await _unitOfWork.CompleteAsync();

            await ClearPostCache(id);
            _logger.LogInformation("Post {PostId} updated successfully.", id);

            return ApiResponse<PostDto>.Success(_mapper.Map<PostDto>(post), "Post updated successfully.");
        }

        public async Task<ApiResponse<bool>> DeletePostAsync(Guid id)
        {
            _logger.LogInformation("Deleting post with ID {PostId}", id);

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser.Data is null)
            {
                _logger.LogWarning("Attempt to delete post {PostId} without a logged-in user.", id);
                return ApiResponse<bool>.Fail("User not authenticated.");
            }

            var post = await _unitOfWork.Posts.GetByIdAsync(id);
            if (post is null) return ApiResponse<bool>.Fail("Post not found.");

            if (post.UserId != currentUser.Data.Id)
            {
                _logger.LogWarning("User {UserId} attempted to delete post {PostId} which they do not own.", currentUser.Data.Id, id);
                return ApiResponse<bool>.Fail("You are not authorized to delete this post.");
            }

            if (!string.IsNullOrEmpty(post.MediaUrl)) await _uploadService.DeleteFileAsync(post.MediaUrl);

            _unitOfWork.Posts.Remove(post);
            await _unitOfWork.CompleteAsync();

            await ClearPostCache(id);
            _logger.LogInformation("Post {PostId} deleted successfully.", id);

            return ApiResponse<bool>.Success(true, "Post deleted successfully.");
        }

        private async Task ClearPostCache(Guid id)
        {
            await _cacheService.RemoveAsync($"post_{id}");
        }

        private Task<Post> HandleTextPost(CreatePostDto createPostDto, Guid userId)
        {
            _logger.LogInformation("Creating text post for user {UserId} with title '{Title}'", userId, createPostDto.Title);

            var post = new Post
            {
                Title = createPostDto.Title,
                Content = createPostDto.Content,
                AttachedSongAlbumArtUrl = createPostDto.AttachedSongAlbumArtUrl,
                AttachedSongArtist = createPostDto.AttachedSongArtist,
                AttachedSongPreviewUrl = createPostDto.AttachedSongPreviewUrl,
                AttachedSongTitle = createPostDto.AttachedSongTitle,
                BackgroundIdentifier = createPostDto.BackgroundIdentifier,
                PostType = PostType.Text,
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            _logger.LogInformation("Text post object created successfully for user {UserId}", userId);

            return Task.FromResult(post);
        }

        private async Task<Post> HandleMediaPost(CreatePostDto createPostDto, Guid userId, bool isVideo)
        {
            _logger.LogInformation("Creating {MediaType} post for user {UserId} with title '{Title}'",
                                   isVideo ? "video" : "image", userId, createPostDto.Title);

            var path = await _uploadService.UploadFileAsync(createPostDto.MediaUrl!, isVideo ? "videos" : "images");
            if (path is null)
            {
                _logger.LogError("File upload failed for user {UserId} and media '{Title}'", userId, createPostDto.Title);
                throw new Exception("File upload failed");
            }

            var post = new Post
            {
                Title = createPostDto.Title,
                Content = createPostDto.Content,
                AttachedSongAlbumArtUrl = createPostDto.AttachedSongAlbumArtUrl,
                AttachedSongArtist = createPostDto.AttachedSongArtist,
                AttachedSongPreviewUrl = createPostDto.AttachedSongPreviewUrl,
                AttachedSongTitle = createPostDto.AttachedSongTitle,
                BackgroundIdentifier = createPostDto.BackgroundIdentifier,
                PostType = isVideo ? PostType.Video : PostType.Image,
                UserId = userId,
                CreatedAt = DateTime.Now,
                MediaUrl = path
            };

            _logger.LogInformation("{MediaType} post object created successfully for user {UserId}",
                                   isVideo ? "Video" : "Image", userId);

            return post;
        }

        public async Task<ApiResponse<IQueryable<PostDto>>> GetPostsByUserIdAsync(Guid userId)
        {
             _logger.LogInformation("Getting posts with UserID {UserId}", userId);

            var cacheKey = $"post_user{userId}";
            var cachedPost = await _cacheService.GetAsync<IQueryable<PostDto>>(cacheKey);
            if (cachedPost != null)
            {
                _logger.LogInformation("Returning posts {UserId} from cache.", userId);
                return ApiResponse<IQueryable<PostDto>>.Success(cachedPost);
            }

            var posts = _unitOfWork.Posts
            .Find(p => p.UserId == userId)
            .Include(p => p.User)
            .Select(p => new PostDto
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content,
                User = _mapper.Map<UserDto>(p.User),
                RepliesCount = p.Replies.Count(),
                ReactionsCount = p.Reactions.Count(),
                CreatedAt = p.CreatedAt,
                AttachedSongAlbumArtUrl = p.AttachedSongAlbumArtUrl,
                AttachedSongArtist = p.AttachedSongArtist,
                AttachedSongPreviewUrl = p.AttachedSongPreviewUrl,
                AttachedSongTitle = p.AttachedSongTitle,
                BackgroundIdentifier = p.BackgroundIdentifier,
                PostType = p.PostType,
                MediaUrl = p.MediaUrl,
                Views = p.Views,
                Shares = p.Shares
            });

            await _cacheService.SetAsync(cacheKey, posts, TimeSpan.FromMinutes(10));

            return ApiResponse<IQueryable<PostDto>>.Success(posts);
        }

        public async Task<ApiResponse<object>> ViewPostAsync(Guid postId)
        {
            _logger.LogInformation("Incrementing view count for post {PostId}", postId);

            var post = await _unitOfWork.Posts.GetByIdAsync(postId);
            if (post is null)
            {
                _logger.LogWarning("Post with ID {PostId} not found for view increment.", postId);
                return ApiResponse<object>.Fail("Post not found.");
            }

            post.Views++;
            _unitOfWork.Posts.Update(post);
            await _unitOfWork.CompleteAsync();

            await ClearPostCache(postId);
            _logger.LogInformation("View count for post {PostId} incremented successfully.", postId);

            return ApiResponse<object>.Success(new { Message = "Post viewed successfully." });
        }

        public async Task<ApiResponse<object>> SharePostAsync(Guid postId)
        {
            _logger.LogInformation("Incrementing share count for post {PostId}", postId);

            var post = await _unitOfWork.Posts.GetByIdAsync(postId);
            if (post is null)
            {
                _logger.LogWarning("Post with ID {PostId} not found for share increment.", postId);
                return ApiResponse<object>.Fail("Post not found.");
            }

            post.Shares++;
            _unitOfWork.Posts.Update(post);
            await _unitOfWork.CompleteAsync();

            await ClearPostCache(postId);
            _logger.LogInformation("Share count for post {PostId} incremented successfully.", postId);

            return ApiResponse<object>.Success(new { Message = "Post shared successfully." });
        }

        public async Task<ApiResponse<IQueryable<PostDto>>> GetPostsAsync(Guid? currentUserId = null)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
            {
                _logger.LogWarning("Attempt to get posts without a logged-in user.");
                return ApiResponse<IQueryable<PostDto>>.Fail("User not authenticated.");
            }

            var userId = currentUserId ?? currentUserResponse.Data.Id;
            _logger.LogInformation("Getting paginated post feed for user {UserId}", userId);

            var followingIds = await _unitOfWork.UserFollows
                .Find(f => f.FollowerId == userId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            // Include the user's own posts in their feed
            followingIds.Add(userId);

            var postsQuery = _unitOfWork.Posts
                .Find(p => followingIds.Contains(p.UserId))
                .Include(p => p.User)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    User = _mapper.Map<UserDto>(p.User),
                    RepliesCount = p.Replies.Count(),
                    ReactionsCount = p.Reactions.Count(),
                    CreatedAt = p.CreatedAt,
                    MediaUrl = p.MediaUrl,
                    Views = p.Views,
                    Shares = p.Shares,
                    AttachedSongAlbumArtUrl = p.AttachedSongAlbumArtUrl,
                    AttachedSongArtist = p.AttachedSongArtist,
                    AttachedSongPreviewUrl = p.AttachedSongPreviewUrl,
                    AttachedSongTitle = p.AttachedSongTitle,
                    BackgroundIdentifier = p.BackgroundIdentifier,
                    PostType = p.PostType,
                });

            return ApiResponse<IQueryable<PostDto>>.Success(postsQuery);
        }

        private async Task SaveMentionsAsync(string content, Guid userId, Guid postId, string name)
        {
            var mentionMatches = Regex.Matches(content, @"@([A-Za-z0-9_\.]+)");

            var mentionedUsernames = mentionMatches
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            var mentionedUsers = await _unitOfWork.Users
                .Find(u => mentionedUsernames.Contains(u.Username))
                .ToListAsync();

            var invalidUsernames = mentionedUsernames
                .Except(mentionedUsers.Select(u => u.Username))
                .ToList();

            if (invalidUsernames.Any())
            {
                var message = $"Invalid mentioned usernames: {string.Join(',', invalidUsernames)}";
                await NotifyUserAsync(message, userId, NotificationType.InvalidMentions);
            }

            foreach (var user in mentionedUsers)
            {
                await _unitOfWork.PostMentions.AddAsync(new PostMention
                {
                    MentionedUserId = user.Id,
                    PostId = postId
                });

                var message = $"{name} mentioned you in a post";
                await NotifyUserAsync(message, user.Id, NotificationType.MentionsSaved);
            }

            await _unitOfWork.CompleteAsync();
        }

        private async Task NotifyUserAsync(string message, Guid userId, NotificationType notificationType)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Message = message,
                NotificationType = notificationType,
                UserId = userId
            });
        }

        public async Task SaveHashtagsAsync(string content, Guid postId)
        {
            var hashtagMatches = Regex.Matches(content, @"#([A-Za-z0-9_]+)");

            var hashtags = hashtagMatches
                .Select(m => m.Groups[1].Value.ToLower())
                .Distinct()
                .ToList();

            if (!hashtags.Any())
                return;

            var existingHashtags = await _unitOfWork.HashTags
                .Find(h => hashtags.Contains(h.Tag))
                .ToListAsync();

            var existingTags = existingHashtags.Select(h => h.Tag).ToHashSet();

           
            var newTags = hashtags.Except(existingTags).ToList();

            // 4. Insert new hashtags
            foreach (var tag in newTags)
            {
                var hashtag = new Hashtag
                {
                    Tag = tag
                };
                await _unitOfWork.HashTags.AddAsync(hashtag);
                existingHashtags.Add(hashtag);
            }

            foreach (var hashtag in existingHashtags)
            {
                var postHashtag = new PostHashtag
                {
                    PostId = postId,
                    HashtagId = hashtag.Id
                };
                await _unitOfWork.PostHashtags.AddAsync(postHashtag);
            }

            await _unitOfWork.CompleteAsync();
        }
    }
}