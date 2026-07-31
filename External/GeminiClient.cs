using BlogGraphQlApp.Settings;
using BlogGraphQlApp.Storage;
using Google.GenAI;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BlogGraphQlApp.External
{
    public sealed class GeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiSettings _geminiSettings;
        private readonly ILogger<GeminiClient> _logger;
        private readonly IFileStorage _fileStorage;

        public GeminiClient(HttpClient httpClient, IOptions<GeminiSettings> options, ILogger<GeminiClient> logger, IFileStorage fileStorage)
        {
            _httpClient = httpClient;
            _logger = logger;
            _geminiSettings = options.Value;
            _fileStorage = fileStorage;

            _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _geminiSettings.ApiKey);
        }

        // === AI Chat Assistant ===
        public async Task<string> GenerateChatAsync(string input)
        {
            //var prompt = $"You are Reelio's AI Assistant. Be concise, helpful, and engaging.\nUser input: {input}";

            var payload = new
            {
                model = "gemini-2.5-pro",
                messages = new[]
                {
                new { role = "user", content = BuildPostPrompt(input) }
            }
            };

            var res = await _httpClient.PostAsJsonAsync("openai/chat/completions", payload);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();

            return json
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;
        }

        // === AI Caption Generator (returns multiple suggestions) ===
        public async Task<List<string>> GenerateCaptionsAsync(string postText, IEnumerable<string> tags)
        {
            var prompt = $@"You are Reelio's AI Caption Generator.
Task: Suggest 3 short, catchy, creative captions for the following post.
Make them engaging and suitable for social media.

Post: {postText}
Tags: {string.Join(", ", tags)}";

            var payload = new
            {
                model = "gemini-2.5-pro",
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            };

            var res = await _httpClient.PostAsJsonAsync("models/gemini-pro:generateContent", payload);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var text = json.GetProperty("candidates")[0]
                           .GetProperty("content")
                           .GetProperty("parts")[0]
                           .GetProperty("text")
                           .GetString() ?? string.Empty;

            // Split captions by newline or delimiter
            var captions = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                               .Select(c => c.Trim())
                               .Where(c => !string.IsNullOrWhiteSpace(c))
                               .ToList();

            return captions;
        }

        // === Moderation System ===
        public async Task<(bool Allowed, List<string> Categories, string Rationale)> ModerateAsync(string content)
        {
            var prompt = $"You are Reelio's Moderation AI. Analyze the following content for safety:\n{content}";

            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                },
                safetySettings = new[]
                {
                    new { category = "HATE_SPEECH", threshold = "BLOCK" },
                    new { category = "HARASSMENT", threshold = "BLOCK" },
                    new { category = "SEXUAL_CONTENT", threshold = "BLOCK" }
                }
            };

            var res = await _httpClient.PostAsJsonAsync("models/gemini-pro:generateContent", payload);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var safety = json.GetProperty("promptFeedback").GetProperty("safetyRatings");

            var blocked = safety.EnumerateArray()
                .Where(r => r.GetProperty("blocked").GetBoolean())
                .Select(r => r.GetProperty("category").GetString() ?? string.Empty)
                .ToList();

            var allowed = blocked.Count == 0;
            var rationale = allowed ? "Passed safety thresholds." : $"Blocked categories: {string.Join(", ", blocked)}";

            return (allowed, blocked, rationale);
        }

        public async Task<List<string>> GenerateImageCaptionsAsync(string relativePath)
        {
            // 1. Read file bytes from file storage (wwwroot in dev, UploadThing in production)
            var bytes = await _fileStorage.DownloadAsync(relativePath);
            var base64 = Convert.ToBase64String(bytes);

            // 2. Build prompt
            var prompt = "You are Reelio's caption generator. Suggest 3 short, catchy captions for this image.";

            // 3. Payload with inline_data
            var payload = new
            {
                contents = new[]
                {
            new {
                role = "user",
                parts = new object[]
                {
                    new { text = prompt },
                    new { inline_data = new { mime_type = "image/jpeg", data = base64 } }
                }
            }
        }
            };

            // 4. Send to Gemini
            var res = await _httpClient.PostAsJsonAsync("models/gemini-pro-vision:generateContent", payload);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var text = json.GetProperty("candidates")[0]
                           .GetProperty("content")
                           .GetProperty("parts")[0]
                           .GetProperty("text")
                           .GetString() ?? string.Empty;

            // 5. Split into multiple captions
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(c => c.Trim())
                       .Where(c => !string.IsNullOrWhiteSpace(c))
                       .ToList();
        }


        public async Task<List<string>> GenerateVideoCaptionsAsync(string relativePath, string transcript)
        {
            // 2. Build prompt with transcript
            var prompt = $@"You are Reelio's caption generator.
Suggest 3 short, catchy captions for this video based on its transcript.

Transcript:
{transcript}";

            var payload = new
            {
                contents = new[]
                {
            new { role = "user", parts = new[] { new { text = prompt } } }
        }
            };

            // 3. Send to Gemini
            var res = await _httpClient.PostAsJsonAsync("models/gemini-pro:generateContent", payload);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var text = json.GetProperty("candidates")[0]
                           .GetProperty("content")
                           .GetProperty("parts")[0]
                           .GetProperty("text")
                           .GetString() ?? string.Empty;

            // 4. Split into multiple captions
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(c => c.Trim())
                       .Where(c => !string.IsNullOrWhiteSpace(c))
                       .ToList();
        }


        public async Task<(bool Allowed, List<string> Categories, string Rationale)> ModerateImageAsync(string relativePath)
        {
            // 1. Read image from file storage (wwwroot in dev, UploadThing in production)
            var bytes = await _fileStorage.DownloadAsync(relativePath);
            var base64 = Convert.ToBase64String(bytes);

            // 2. Build prompt
            var prompt = "You are Reelio's moderation AI. Analyze this image for unsafe or harmful content.";

            // 3. Payload with inline_data
            var payload = new
            {
                contents = new[]
                {
            new {
                role = "user",
                parts = new object[]
                {
                    new { text = prompt },
                    new { inline_data = new { mime_type = "image/jpeg", data = base64 } }
                }
            }
        },
                safetySettings = new[]
                {
            new { category = "HATE_SPEECH", threshold = "BLOCK" },
            new { category = "HARASSMENT", threshold = "BLOCK" },
            new { category = "SEXUAL_CONTENT", threshold = "BLOCK" },
            new { category = "VIOLENCE", threshold = "BLOCK" }
        }
            };

            // 4. Send to Gemini Vision
            var res = await _httpClient.PostAsJsonAsync("models/gemini-pro-vision:generateContent", payload);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var safety = json.GetProperty("promptFeedback").GetProperty("safetyRatings");

            var blocked = safety.EnumerateArray()
                .Where(r => r.GetProperty("blocked").GetBoolean())
                .Select(r => r.GetProperty("category").GetString() ?? string.Empty)
                .ToList();

            var allowed = blocked.Count == 0;
            var rationale = allowed ? "Passed safety thresholds." : $"Blocked categories: {string.Join(", ", blocked)}";

            return (allowed, blocked, rationale);
        }

        public async Task<(bool Allowed, List<string> Categories, string Rationale)> ModerateVideoAsync(
        string transcript,
        List<string> framePaths)
        {
            var blockedCategories = new List<string>();
            var rationaleParts = new List<string>();

            // 1. Moderate transcript as text
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                var (allowedText, catsText, rationaleText) = await ModerateAsync(transcript);
                if (!allowedText) blockedCategories.AddRange(catsText);
                rationaleParts.Add($"Transcript moderation: {rationaleText}");
            }

            // 2. Moderate key frames as images
            foreach (var framePath in framePaths)
            {
                var (allowedImg, catsImg, rationaleImg) = await ModerateImageAsync(framePath);
                if (!allowedImg) blockedCategories.AddRange(catsImg);
                rationaleParts.Add($"Frame {framePath} moderation: {rationaleImg}");
            }

            // 3. Combine results
            var allowed = blockedCategories.Count == 0;
            var rationale = string.Join(" | ", rationaleParts);

            return (allowed, blockedCategories.Distinct().ToList(), rationale);
        }

        private string BuildPostPrompt(string input)
        {
            return $@"
You are Reelio's AI Assistant. 
You create powerful, engaging social media content based on a title or user prompt.

RULES:
- Start with a powerful HOOK.
- Write in a modern, emotional, human tone.
- Use short paragraphs.
- Keep it skimmable (like a reel voiceover or caption).
- Format cleanly, no emojis unless they fit the tone naturally.
- DO NOT mention that you are an AI.
- If the input is short (like a title), expand it into a full post.
- If the input is long (like a description), follow it but improve the writing.

USER INPUT:
""{input}""

Now produce the complete post content:
";
        }


    }
}
