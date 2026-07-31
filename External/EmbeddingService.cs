using OpenAI;
using Polly;
using Polly.Retry;
using System.Net;

//using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlogGraphQlApp.Storage;


namespace BlogGraphQlApp.External
{
    public class EmbeddingService
    {
        private readonly OpenAIClient _openAi;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IFileStorage _fileStorage;

        public EmbeddingService(HttpClient httpClient,OpenAIClient openAi, IConfiguration config, IFileStorage fileStorage)
        {
            _httpClient = httpClient;
            _openAi = openAi ?? throw new ArgumentNullException(nameof(openAi));
            _apiKey = config["OpenAI:ApiKey"]!;
            _fileStorage = fileStorage;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Generate a text embedding using OpenAI's text-embedding-3-small model
        /// </summary>
        /// <param name="text"></param>
        public async Task<float[]> CreateTextEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));

            var embeddingClient = _openAi.GetEmbeddingClient("text-embedding-3-small");

            var response = await embeddingClient.GenerateEmbeddingAsync(text);

            // Returns the float array of the embedding
            var vector = response.Value.ToFloats().ToArray();

            // Always ensure 1536 dimensions
            return NormalizeVectorTo1536(vector);
        }

        /// <summary>
        /// Generate an embedding for media (image/video) as base64 string
        /// </summary>
        /// <param name="vector">todo: describe vector parameter on NormalizeVectorTo1536</param>
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

        public async Task<float[]> CreateMediaEmbeddingAsync(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                throw new ArgumentException("Base64 cannot be null or empty.", nameof(base64));

            // Make sure the base64 string includes the data URL prefix
            if (!base64.StartsWith("data:image"))
                base64 = $"data:image/jpeg;base64,{base64}";

            // Prompt instructing the model to return a numerical vector for similarity search
            var systemPrompt = """
You are an AI assistant that converts images into numerical vectors suitable for similarity search.
Return ONLY a JSON array of floats (e.g., [0.123, -0.456, ...]) corresponding to the image embedding.
Do NOT include any text, explanation, or extra symbols.
""";

            var userPrompt = $"Here is the image in base64 format: {base64}";

            var payload = new
            {
                model = "gpt-4o-mini", // or whichever model your key has access to
                messages = new[]
                {
    new { role = "system", content = systemPrompt },
    new { role = "user", content = userPrompt }
},
                temperature = 0.0,
                max_tokens = 2000
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(raw);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                throw new Exception("No response from OpenAI.");

            var message = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            // Extract JSON array from response
            var start = message.IndexOf('[');
            var end = message.LastIndexOf(']');
            if (start < 0 || end <= start)
                throw new Exception("No JSON array found in OpenAI response.");

            var jsonArray = message[start..(end + 1)];

            try
            {
                var vector = JsonSerializer.Deserialize<float[]>(jsonArray)
                    ?? throw new Exception("Failed to deserialize embedding.");
                // Normalize to 1536
                var normalized = NormalizeVectorTo1536(vector);

                return normalized;
                       
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse embedding from OpenAI response.", ex);
            }
        }



        /// <summary>
        /// Combine multiple embeddings into one vector
        /// </summary>
        /// <param name="embeddings">todo: describe embeddings parameter on Combine</param>
        public float[] Combine(params float[][] embeddings)
        {
            if (embeddings == null || embeddings.Length == 0)
                throw new ArgumentException("At least one embedding is required.", nameof(embeddings));

            var combined = embeddings[0];

            for (int i = 1; i < embeddings.Length; i++)
            {
                if (embeddings[i].Length != combined.Length)
                    throw new InvalidOperationException("All embeddings must have the same length.");

                combined = combined.Zip(embeddings[i], (a, b) => (a + b) / 2f).ToArray();
            }

            return combined;
        }
    
        public async Task<float[]> CreateVideoEmbeddingWithMiniAsync(string base64Video)
        {
            if (string.IsNullOrWhiteSpace(base64Video))
                throw new ArgumentException("Base64 cannot be null or empty.", nameof(base64Video));

            if (!base64Video.StartsWith("data:video"))
                base64Video = $"data:video/mp4;base64,{base64Video}";

            var systemPrompt = """
    You are an AI assistant that converts media content into numerical embedding vectors for similarity search.
    The user will provide a video in base64 format.
    Analyze the visual content of the video and produce a compact numerical vector representation.

    Return ONLY a JSON array of floats.
    """;

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Here is the video in base64 format: {base64Video}" }
            },
                temperature = 0.0,
                max_tokens = 4000
            };

            var json = JsonSerializer.Serialize(payload);
            var raw = string.Empty;

            var retryPolicy =
                    Policy<HttpResponseMessage>
                        .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                        .WaitAndRetryAsync(
                            5,
                            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                        );


            var finalResponse = await retryPolicy.ExecuteAsync(async () =>
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                return await _httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    content);
            });

            finalResponse.EnsureSuccessStatusCode();
            raw = await finalResponse.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(raw);
            var message = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            var start = message.IndexOf('[');
            var end = message.LastIndexOf(']');
            if (start < 0 || end <= start)
                throw new Exception("No JSON array found in OpenAI response.");

            var jsonArray = message[start..(end + 1)];
            var vector = JsonSerializer.Deserialize<float[]>(jsonArray)
                ?? throw new Exception("Failed to parse embedding.");

            return NormalizeVectorTo1536(vector);
        }




    /// <summary>
    /// Helper to convert a file (image/video) into base64
    /// </summary>
    /// <param name="filePath">The stored URL of the file to convert.</param>
    public async Task<string> ConvertFileToBase64Async(string filePath)
        {
            var bytes = await _fileStorage.DownloadAsync(filePath);
            return Convert.ToBase64String(bytes);
        }
    }
}
