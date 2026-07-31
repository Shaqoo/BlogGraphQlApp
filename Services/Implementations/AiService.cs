using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.External;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Implementations
{
    public class AiService : IAiService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GeminiClient _gemini;

        public AiService(IUnitOfWork unitOfWork, GeminiClient gemini)
        {
            _unitOfWork = unitOfWork;
            _gemini = gemini;
        }

        private async Task<bool> CheckLimitAsync(Guid userId, string feature)
        {
            var usage = await _unitOfWork.AiUsages
                .Find(u => u.UserId == userId).FirstOrDefaultAsync();

            if (usage == null)
            {
                usage = new AiUsage { UserId = userId };
                await _unitOfWork.AiUsages.AddAsync(usage);
            }

            var exceeded = usage.RequestCount >= 5;
            if (exceeded) return false;

            usage.RequestCount++;
            usage.LastUsedAt = DateTime.UtcNow;

            switch (feature)
            {
                case "chat": usage.ChatRequests++; break;
                case "caption": usage.CaptionRequests++; break;
                default:
                    throw new Exception("Unexpected Case");
            }

            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<string?> ChatAsync(Guid userId, string input)
        {
            if (!await CheckLimitAsync(userId, "chat"))
                return "AI usage limit reached (5 requests).";

            return await _gemini.GenerateChatAsync(input);
        }

        public async Task<List<string>> CaptionAsync(Guid userId, Guid postId)
        {
            if (!await CheckLimitAsync(userId, "caption"))
                return new List<string> { "AI usage limit reached (5 requests)." };

            var post = await _unitOfWork.Posts.GetByIdAsync(postId);
            if (post == null) return new List<string> { "Post Not Found" };

            var tags = await _unitOfWork.PostHashtags
                .Find(a => a.PostId == post.Id)
                .Include(a => a.Hashtag)
                .Select(b => b.Hashtag.Tag)
                .ToListAsync();

            return post.PostType switch
            {
                PostType.Video => await _gemini.GenerateVideoCaptionsAsync(
                    post.MediaUrl ?? string.Empty,
                    transcript: post.Transcript ?? string.Empty
                ),

                PostType.Text => await _gemini.GenerateCaptionsAsync(
                    post.Content ?? string.Empty,
                    tags ?? new List<string>()
                ),

                PostType.Image => await _gemini.GenerateImageCaptionsAsync(
                    post.MediaUrl ?? string.Empty
                ),

                _ => throw new GraphQLException("Invalid Post Type")
            };
        }


        public async Task<ModerationResult?> ModerateAsync(Guid postId)
        {
            var post = await _unitOfWork.Posts.GetByIdAsync(postId);
            if (post == null) return new ModerationResult { Allowed = false, Rationale = "Post Not Found" };

            switch (post.PostType)
            {
                case PostType.Text:
                    {
                        var (allowed, cats, rationale) = await _gemini.ModerateAsync(post.Content ?? string.Empty);
                        return new ModerationResult
                        {
                            Allowed = allowed,
                            Categories = ConvertCategories(cats),
                            Rationale = rationale,
                            MediaType = PostType.Text,
                            PostId = post.Id
                        };
                    }

                case PostType.Image:
                    {
                        var (allowed, cats, rationale) = await _gemini.ModerateImageAsync(post.MediaUrl ?? string.Empty);
                        return new ModerationResult
                        {
                            Allowed = allowed,
                            Categories = ConvertCategories(cats),
                            Rationale = rationale,
                            MediaType = PostType.Image,
                            PostId = post.Id
                        };
                    }

                case PostType.Video:
                    {
                        var (allowed, cats, rationale) = await _gemini.ModerateVideoAsync(
                            post.Transcript ?? string.Empty,
                            post.FramePaths ?? new List<string>()
                        );
                        return new ModerationResult
                        {
                            Allowed = allowed,
                            Categories = ConvertCategories(cats),
                            Rationale = rationale,
                            MediaType = PostType.Video,
                            PostId = post.Id
                        };
                    }

                default:
                    throw new GraphQLException("Invalid Post Type");
            }
        }

        public static List<ModerationCategory> ConvertCategories(List<string> cats)
        {
            var results = new List<ModerationCategory>();

            foreach (var cat in cats)
            {
                switch (cat.Trim().ToUpperInvariant())
                {
                    case "HATE_SPEECH":
                        results.Add(ModerationCategory.HateSpeech);
                        break;
                    case "HARASSMENT":
                        results.Add(ModerationCategory.Harassment);
                        break;
                    case "SEXUAL_CONTENT":
                        results.Add(ModerationCategory.SexualContent);
                        break;
                    case "VIOLENCE":
                        results.Add(ModerationCategory.Violence);
                        break;
                    case "NONE":
                        results.Add(ModerationCategory.None);
                        break;
                    default:
                        results.Add(ModerationCategory.Other);
                        break;
                }
            }

            return results;
        }
    }

}
