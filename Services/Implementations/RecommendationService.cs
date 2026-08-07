using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
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
        private readonly IMapper _mapper;
        private readonly ILogger<RecommendationService> _logger;
        private readonly MLContext _mlContext;

        public RecommendationService(IUnitOfWork unitOfWork, IAuthService authService, IMapper mapper, ILogger<RecommendationService> logger)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _mapper = mapper;
            _logger = logger;
            _mlContext = new MLContext();
        }

        public async Task<ApiResponse<IEnumerable<PostDto>>> GetPostRecommendationsAsync(int limit = 10)        {
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

        public async Task<ApiResponse<IEnumerable<ReelDto>>> GetReelRecommendationsAsync(int limit = 10)        {
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
    }
}