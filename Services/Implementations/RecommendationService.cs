using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.External;
using BlogGraphQlApp.ML;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly EmbeddingService _embeddingService;
        private readonly PineconeService _pinecone;
        private readonly IMapper _mapper;
        private readonly ILogger<RecommendationService> _logger;
        private readonly MLContext _mlContext;

        public RecommendationService(IUnitOfWork unitOfWork, IAuthService authService, EmbeddingService embeddingService, PineconeService pinecone, IMapper mapper, ILogger<RecommendationService> logger)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _embeddingService = embeddingService;
            _pinecone = pinecone;
            _mapper = mapper;
            _logger = logger;
            _mlContext = new MLContext();
        }

        public async Task<ApiResponse<IEnumerable<PostDto>>> GetPostRecommendationsAsync(int limit = 10)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
            {
                return ApiResponse<IEnumerable<PostDto>>.Fail("User not authenticated.");
            }

            var userId = currentUserResponse.Data.Id;
            _logger.LogInformation("Generating post recommendations for user {UserId}", userId);

            var interactions = await _unitOfWork.UserInteractions.Find(i => i.PostId.HasValue).ToListAsync();
            if (interactions.Count < 10) // Not enough data to train
            {
                _logger.LogWarning("Not enough interaction data to generate post recommendations.");
                return ApiResponse<IEnumerable<PostDto>>.Success([], "Not enough data for recommendations. Explore more to get personalized content!");
            }

            var trainingData = interactions.Select(i => new ContentRating
            {
                UserId = BitConverter.ToInt32(i.UserId.ToByteArray(), 0),
                ContentId = BitConverter.ToInt32(i.PostId!.Value.ToByteArray(), 0),
                Label = CalculateRating(i)
            }).ToList();

            var predictionEngine = TrainModel(trainingData);

            var allPosts = await _unitOfWork.Posts.GetAll().ToListAsync();
            var userInteractedPostIds = new HashSet<Guid>(interactions.Where(i => i.UserId == userId).Select(i => i.PostId!.Value));

            var recommendations = new List<(PostDto Post, float Score)>();

            foreach (var post in allPosts)
            {
                if (!userInteractedPostIds.Contains(post.Id))
                {
                    var prediction = predictionEngine.Predict(
                        new ContentRating
                        {
                            UserId = BitConverter.ToInt32(userId.ToByteArray(), 0),
                            ContentId = BitConverter.ToInt32(post.Id.ToByteArray(), 0)
                        });

                    if (!float.IsNaN(prediction.Score))
                    {
                        recommendations.Add((_mapper.Map<PostDto>(post), prediction.Score));
                    }
                }
            }

            var topRecommendations = recommendations.OrderByDescending(r => r.Score).Take(limit).Select(r => r.Post);
            return ApiResponse<IEnumerable<PostDto>>.Success(topRecommendations);
        }

        public async Task<ApiResponse<IEnumerable<ReelDto>>> GetReelRecommendationsAsync(int limit = 10)
        {
            var currentUserResponse = await _authService.GetCurrentUserAsync();
            if (!currentUserResponse.Succeeded || currentUserResponse.Data == null)
            {
                return ApiResponse<IEnumerable<ReelDto>>.Fail("User not authenticated.");
            }

            var userId = currentUserResponse.Data.Id;
            _logger.LogInformation("Generating reel recommendations for user {UserId}", userId);

            var interactions = await _unitOfWork.UserInteractions.Find(i => i.ReelId.HasValue).ToListAsync();
            if (interactions.Count < 10)
            {
                _logger.LogWarning("Not enough interaction data to generate reel recommendations.");
                return ApiResponse<IEnumerable<ReelDto>>.Success(new List<ReelDto>(), "Not enough data for recommendations. Explore more to get personalized content!");
            }

            var trainingData = interactions.Select(i => new ContentRating
            {
                UserId = BitConverter.ToInt32(i.UserId.ToByteArray(), 0),
                ContentId = BitConverter.ToInt32(i.ReelId!.Value.ToByteArray(), 0),
                Label = CalculateRating(i)
            }).ToList();

            var predictionEngine = TrainModel(trainingData);

            var allReels = await _unitOfWork.Reels.GetAll().ToListAsync();
            var userInteractedReelIds = new HashSet<Guid>(interactions.Where(i => i.UserId == userId).Select(i => i.ReelId!.Value));

            var recommendations = new List<(ReelDto Reel, float Score)>();

            foreach (var reel in allReels)
            {
                if (!userInteractedReelIds.Contains(reel.Id))
                {
                    var prediction = predictionEngine.Predict(
                        new ContentRating
                        {
                            UserId = BitConverter.ToInt32(userId.ToByteArray(), 0),
                            ContentId = BitConverter.ToInt32(reel.Id.ToByteArray(), 0)
                        });

                    if (!float.IsNaN(prediction.Score))
                    {
                        recommendations.Add((_mapper.Map<ReelDto>(reel), prediction.Score));
                    }
                }
            }

            var topRecommendations = recommendations.OrderByDescending(r => r.Score).Take(limit).Select(r => r.Reel);
            return ApiResponse<IEnumerable<ReelDto>>.Success(topRecommendations);
        }

        private PredictionEngine<ContentRating, ContentRatingPrediction> TrainModel(List<ContentRating> ratings)
        {
            var trainingDataView = _mlContext.Data.LoadFromEnumerable(ratings);

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserId",
                MatrixRowIndexColumnName = "ContentId",
                LabelColumnName = "Label",
                NumberOfIterations = 20,
                ApproximationRank = 100
            };

            var trainer = _mlContext.Recommendation().Trainers.MatrixFactorization(options);
            var model = trainer.Fit(trainingDataView);

            return _mlContext.Model.CreatePredictionEngine<ContentRating, ContentRatingPrediction>(model);
        }

        private static float CalculateRating(UserInteraction interaction)
        {
            // Simple rating: 1 point for every 30s, 5 points for a favorite.
            var rating = (interaction.TimeSpentInSeconds / 30.0f) + (interaction.IsFavorite ? 5.0f : 0.0f);
            return Math.Max(1.0f, rating); // Ensure rating is at least 1.
        }

        public async Task<PaginatedResult<PostDto>> GetRecommendedPostsAsync(
          Guid userId,
          int page = 1,
          int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Max(pageSize, 10);

            var skip = (page - 1) * pageSize;
            var take = page * pageSize; // fetch extra for Pinecone

            // 1. Load user interactions (posts and reels)
            var interactions = await _unitOfWork.UserInteractions
                .Find(x => x.UserId == userId)
                .Include(x => x.Post)
                .Include(x => x.Reel)
                .ToListAsync();

            var count = await CountAsync();

            if (!interactions.Any())
                return PaginatedResult<PostDto>.Create(await GetRandomPostsPagedAsync(skip, pageSize), page, pageSize,count);

            // 2. Build user embedding dynamically
            var userVector = await BuildUserEmbeddingAsync(interactions);
            if (userVector == null)
                return PaginatedResult<PostDto>.Create(await GetRandomPostsPagedAsync(skip, pageSize), page, pageSize,count);

            // 3. Query Pinecone for recommendations
            var postIds = (await _pinecone.QueryAsync(userVector, take))
                .Distinct()
                .ToList();

            if (!postIds.Any())
                return PaginatedResult<PostDto>.Create(await GetRandomPostsPagedAsync(skip, pageSize), page, pageSize, count);

            // 4. Pagination in memory
            var pagedIds = postIds.Skip(skip).Take(pageSize).ToList();

            // 5. Load posts from DB in same order
            var posts = await _unitOfWork.Posts
                .Find(p => pagedIds.Contains(p.Id.ToString()))
                .Select(p => new PostDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    RepliesCount = p.Replies.Count(),
                    ReactionsCount = p.Reactions.Count(),
                    CreatedAt = p.CreatedAt,
                    UserId = p.UserId,
                    MediaUrl = p.MediaUrl,
                    Views = p.Views,
                    Shares = p.Shares,
                    AttachedSongAlbumArtUrl = p.AttachedSongAlbumArtUrl,
                    AttachedSongArtist = p.AttachedSongArtist,
                    AttachedSongPreviewUrl = p.AttachedSongPreviewUrl,
                    AttachedSongTitle = p.AttachedSongTitle,
                    BackgroundIdentifier = p.BackgroundIdentifier,
                    PostType = p.PostType,
                })
                .ToListAsync();

            posts = pagedIds.Select(id => posts.First(p => p.Id.ToString() == id)).ToList();

            if (!posts.Any())
                return PaginatedResult<PostDto>.Create(await GetRandomPostsPagedAsync(skip, pageSize), page, pageSize, count);

            return PaginatedResult<PostDto>.Create(posts, page, pageSize, count);
        }

        private async Task<float[]?> BuildUserEmbeddingAsync(List<UserInteraction> interactions)
        {
            if (!interactions.Any())
                return null;

            var weightedVectors = new List<(float[] Vector, float Weight)>();

            foreach (var interaction in interactions)
            {
                float[]? vector = null;
                if (interaction.Post != null)
                {
                    // Text post embedding
                    vector = await _embeddingService.CreateTextEmbeddingAsync(
                        interaction.Post.Title + " " + interaction.Post.Content);
                }
                //else if (interaction.Reel != null)
                //{
                //    // Media embedding
                //    vector = await _embeddingService.CreateMediaEmbeddingAsync(interaction.Reel.Base64Media);
                //}

                if (vector != null)
                {
                    var weight = CalculateWeight(interaction);
                    weightedVectors.Add((vector, weight));
                }
            }

            if (!weightedVectors.Any())
                return null;

            // Weighted average
            var dimension = weightedVectors[0].Vector.Length;
            var result = new float[dimension];
            float totalWeight = 0;

            foreach (var item in weightedVectors)
            {
                for (int i = 0; i < dimension; i++)
                    result[i] += item.Vector[i] * item.Weight;
                totalWeight += item.Weight;
            }

            for (int i = 0; i < dimension; i++)
                result[i] /= totalWeight;

            return result;
        }

        private float CalculateWeight(UserInteraction interaction)
        {
            var weight = 1f;
            weight += interaction.TimeSpentInSeconds / 30f;
            if (interaction.IsFavorite)
                weight *= 2f;
            weight *= (1f - interaction.DecayRate);
            return weight;
        }

        private async Task<List<PostDto>> GetRandomPostsPagedAsync(int skip, int take)
        {
            var posts = await _unitOfWork.Posts.GetAll()
                .OrderBy(x => Guid.NewGuid())
                .Skip(skip)
                .Take(take)
                .Select(p => new PostDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    RepliesCount = p.Replies.Count(),
                    ReactionsCount = p.Reactions.Count(),
                    CreatedAt = p.CreatedAt,
                    MediaUrl = p.MediaUrl,
                    Views = p.Views,
                    UserId = p.UserId,
                    Shares = p.Shares,
                    AttachedSongAlbumArtUrl = p.AttachedSongAlbumArtUrl,
                    AttachedSongArtist = p.AttachedSongArtist,
                    AttachedSongPreviewUrl = p.AttachedSongPreviewUrl,
                    AttachedSongTitle = p.AttachedSongTitle,
                    BackgroundIdentifier = p.BackgroundIdentifier,
                    PostType = p.PostType,
                }).ToListAsync();

            return posts;
        }
        private Task<int> CountAsync()
        {
            return _unitOfWork.Posts.CountAsync(a => !a.IsDeleted);
        }
    }
}