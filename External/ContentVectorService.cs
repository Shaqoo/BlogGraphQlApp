using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.External;


public class ContentVectorService
{
    private readonly EmbeddingService _embeddingService;
    private readonly PineconeService _pineconeService;

    public ContentVectorService(EmbeddingService embeddingService, PineconeService pineconeService)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _pineconeService = pineconeService ?? throw new ArgumentNullException(nameof(pineconeService));
    }
    public float[] NormalizeVectorTo1536(float[] vector)
    {
        const int targetDim = 1536;
        if (vector.Length == targetDim)
            return vector;

        var normalized = new float[targetDim];

        if (vector.Length > targetDim)
        {
            // Truncate
            Array.Copy(vector, normalized, targetDim);
        }
        else
        {
            // Copy existing values, leave remaining as 0
            Array.Copy(vector, normalized, vector.Length);
        }

        return normalized;
    }

    /// <summary>
    /// Generate vector for a post (text + optional media) and store in Pinecone
    /// </summary>
    /// <param name="post">todo: describe post parameter on UpsertPostAsync</param>
    public async Task UpsertPostAsync(Post post)
    {
        if (post == null) throw new ArgumentNullException(nameof(post));

        // Generate text embedding
        var textVector = await _embeddingService.CreateTextEmbeddingAsync(post.Title + " " + (post.Content ?? ""));

        var combinedVector = textVector;


        if (!string.IsNullOrEmpty(post.MediaUrl))
        {
            try
            {
                var base64 = await _embeddingService.ConvertFileToBase64Async(post.MediaUrl);

                var mediaVector = post.PostType == Enums.PostType.Image
                    ? await _embeddingService.CreateMediaEmbeddingAsync(base64)
                    : await _embeddingService.CreateVideoEmbeddingWithMiniAsync(base64);

                combinedVector = _embeddingService.Combine(textVector, mediaVector);
            }
            catch (Exception ex)
            { 
                Console.WriteLine(
                    $"[Embedding Warning] Post {post.Id} media embedding failed: {ex.Message}");
            }
        }

        Console.WriteLine(combinedVector);
        Console.WriteLine(new
        {
            type = nameof(Post),
            title = post.Title,
            userId = post.UserId,
            mediaUrl = post.MediaUrl
        });

        Console.WriteLine($"DEBUG: Vector Length is {combinedVector.Length}");
        combinedVector = NormalizeVectorTo1536(combinedVector);
        Console.WriteLine($"DEBUG: Vector Length is {combinedVector.Length}");


        await _pineconeService.UpsertAsync(
            id: post.Id.ToString(),
            vector: combinedVector,
            metadata: new
            {
                type = nameof(Post),
                title = post.Title,
                userId = post.UserId,
                mediaUrl = post.MediaUrl ?? "No Url"
            }
        );
    }

    /// <summary>
    /// Generate vector for a media-only post (image/video)
    /// </summary>
    /// <param name="postId">todo: describe postId parameter on UpsertMediaAsync</param>
    /// <param name="mediaPath">todo: describe mediaPath parameter on UpsertMediaAsync</param>
    /// <param name="title">todo: describe title parameter on UpsertMediaAsync</param>
    /// <param name="userId">todo: describe userId parameter on UpsertMediaAsync</param>
    public async Task UpsertMediaAsync(Guid postId, string mediaPath, string title = "", Guid? userId = null)
    {
        var base64 = await _embeddingService.ConvertFileToBase64Async(mediaPath);
        var mediaVector = await _embeddingService.CreateMediaEmbeddingAsync(base64);

        await _pineconeService.UpsertAsync(
            id: postId.ToString(),
            vector: mediaVector,
            metadata: new
            {
                type = "post",
                title,
                userId,
                mediaUrl = mediaPath
            }
        );
    }


}
