using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Dtos;
using BlogGraphQlApp.DTOs;
using FluentValidation;
using HotChocolate.Authorization;

[ExtendObjectType("Mutation")]
public class PostMutation
{
    private readonly ILogger<PostMutation> _logger;

    public PostMutation(ILogger<PostMutation> logger)
    {
        _logger = logger;
    }

    [Authorize]
    public async Task<ApiResponse<PostDto>> CreatePostAsync(
        CreatePostDto createPostDto,
        [Service] IPostService postService,
        [Service] IValidator<CreatePostDto> validator)
    {
        try
        {
            var validationResult = await validator.ValidateAsync(createPostDto);
            if (!validationResult.IsValid)
            {
                return ApiResponse<PostDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            _logger.LogInformation("User creating a post with title '{Title}'", createPostDto.Title);

            var response = await postService.CreatePostAsync(createPostDto);

            if (!response.Succeeded)
            {
                _logger.LogWarning("Post creation failed: {Message}", response.Message);
            }
            else
            {
                _logger.LogInformation("Post {PostId} created successfully", response.Data?.Id);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating post with title '{Title}'", createPostDto.Title);
            return ApiResponse<PostDto>.Fail("An unexpected error occurred.");
        }
    }

    [Authorize]
    public async Task<ApiResponse<PostDto>> UpdatePostAsync(
        UpdatePostDto updatePostDto,
        Guid id,
        [Service] IPostService postService,
        [Service] IValidator<UpdatePostDto> validator)
    {
        try
        {
            var validationResult = await validator.ValidateAsync(updatePostDto);
            if (!validationResult.IsValid)
            {
                return ApiResponse<PostDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            _logger.LogInformation("User updating post {PostId}", id);

            var response = await postService.UpdatePostAsync(id, updatePostDto);

            if (!response.Succeeded)
            {
                _logger.LogWarning("Post update failed for {PostId}: {Message}", id, response.Message);
            }
            else
            {
                _logger.LogInformation("Post {PostId} updated successfully", id);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating post {PostId}", id);
            return ApiResponse<PostDto>.Fail("An unexpected error occurred.");
        }
    }

    [Authorize]
    public async Task<ApiResponse<bool>> DeletePostAsync(
        Guid id,
        [Service] IPostService postService)
    {
        try
        {
            _logger.LogInformation("User deleting post {PostId}", id);
            return await postService.DeletePostAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting post {PostId}", id);
            return ApiResponse<bool>.Fail("An unexpected error occurred while deleting the post.");
        }
    }

    public async Task<ApiResponse<object>> ViewPostAsync(Guid postId, [Service] IPostService postService)
    {
        _logger.LogInformation("Viewing post {PostId}", postId);
        return await postService.ViewPostAsync(postId);
    }

    public async Task<ApiResponse<object>> SharePostAsync(Guid postId, [Service] IPostService postService)
    {
        _logger.LogInformation("Sharing post {PostId}", postId);
        return await postService.SharePostAsync(postId);
    }
}
