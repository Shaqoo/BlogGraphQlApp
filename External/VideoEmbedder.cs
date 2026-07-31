using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Accord.Video;

namespace BlogGraphQlApp.External
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Xabe.FFmpeg;

    public class VideoEmbedder
    {
        private readonly HttpClient _httpClient;

        public VideoEmbedder(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Main method: generates a single embedding for a video file.
        /// </summary>
        /// <param name="videoPath">todo: describe videoPath parameter on CreateVideoEmbeddingAsync</param>
        /// <param name="frameRate">todo: describe frameRate parameter on CreateVideoEmbeddingAsync</param>
        public async Task<float[]> CreateVideoEmbeddingAsync(string videoPath, int frameRate = 1)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"VideoFrames_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var frameFiles = await ExtractFramesAsync(videoPath, tempDir, frameRate);
                var frameEmbeddings = new List<float[]>();

                foreach (var frameFile in frameFiles)
                {
                    var base64 = FileToBase64(frameFile);
                    var embedding = await CreateMediaEmbeddingAsync(base64);
                    frameEmbeddings.Add(embedding);
                }

                return AverageEmbeddings(frameEmbeddings);
            }
            finally
            {
                // Clean up temporary files
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Extract frames from video using Xabe.FFmpeg
        /// </summary>
        /// <param name="videoPath">todo: describe videoPath parameter on ExtractFramesAsync</param>
        /// <param name="outputDir">todo: describe outputDir parameter on ExtractFramesAsync</param>
        /// <param name="frameRate">todo: describe frameRate parameter on ExtractFramesAsync</param>
        private async Task<string[]> ExtractFramesAsync(string videoPath, string outputDir, int frameRate = 1)
        {
            // Set FFmpeg executables path if needed, or leave empty if ffmpeg is in PATH
            FFmpeg.SetExecutablesPath("");

            var snapshotFiles = new List<string>();
            var mediaInfo = await FFmpeg.GetMediaInfo(videoPath);
            var durationSeconds = mediaInfo.Duration.TotalSeconds;

            for (double t = 0; t < durationSeconds; t += 1.0 / frameRate)
            {
                var outputFile = Path.Combine(outputDir, $"frame-{t:F2}.jpg");
                var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(videoPath, outputFile, TimeSpan.FromSeconds(t));
                await conversion.Start();
                snapshotFiles.Add(outputFile);
            }

            return snapshotFiles.ToArray();
        }

        /// <summary>
        /// Convert a file to base64
        /// </summary>
        /// <param name="path">todo: describe path parameter on FileToBase64</param>
        private string FileToBase64(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Generate a single embedding for a base64 image using OpenAI API.
        /// </summary>
        /// <param name="base64">todo: describe base64 parameter on CreateMediaEmbeddingAsync</param>
        private async Task<float[]> CreateMediaEmbeddingAsync(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                throw new ArgumentException("Base64 cannot be null or empty.", nameof(base64));

            if (!base64.StartsWith("data:image"))
                base64 = $"data:image/jpeg;base64,{base64}";

            var systemPrompt = """
You are an AI assistant that converts images into numerical vectors suitable for similarity search.
Return ONLY a JSON array of floats (e.g., [0.123, -0.456, ...]) corresponding to the image embedding.
Do NOT include any text, explanation, or extra symbols.
""";

            var userPrompt = $"Here is the image in base64 format: {base64}";

            var payload = new
            {
                model = "gpt-4o-mini",
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
            var start = message.IndexOf('[');
            var end = message.LastIndexOf(']');
            if (start < 0 || end <= start)
                throw new Exception("No JSON array found in OpenAI response.");

            var jsonArray = message[start..(end + 1)];
            var vector = JsonSerializer.Deserialize<float[]>(jsonArray)
                ?? throw new Exception("Failed to deserialize embedding.");

            return NormalizeVectorTo1536(vector);
        }

        /// <summary>
        /// Average a list of vectors into one.
        /// </summary>
        /// <param name="embeddings">todo: describe embeddings parameter on AverageEmbeddings</param>
        private float[] AverageEmbeddings(List<float[]> embeddings)
        {
            var length = embeddings[0].Length;
            var avg = new float[length];

            foreach (var vec in embeddings)
            {
                for (int i = 0; i < length; i++)
                    avg[i] += vec[i];
            }


            for (int i = 0; i < length; i++)
                avg[i] /= embeddings.Count;

            return avg;
        }

        /// <summary>
        /// Normalize to 1536 dimensions (dummy implementation; adjust as needed)
        /// </summary>
        /// <param name="vector">todo: describe vector parameter on NormalizeVectorTo1536</param>
        private float[] NormalizeVectorTo1536(float[] vector)
        {
            if (vector.Length == 1536) return vector;

            var result = new float[1536];
            for (int i = 0; i < result.Length; i++)
                result[i] = vector[i % vector.Length];
            return result;
        }

        public async Task<float[]> CreateVideoEmbeddingFromBase64Async(string base64Video, int frameRate = 1)
        {
            if (string.IsNullOrWhiteSpace(base64Video))
                throw new ArgumentException("Base64 video string is empty.", nameof(base64Video));

            // Strip data URI prefix if present
            var commaIndex = base64Video.IndexOf(',');
            if (commaIndex >= 0)
                base64Video = base64Video[(commaIndex + 1)..];

            // Convert base64 → bytes
            var bytes = Convert.FromBase64String(base64Video);

            // Create a temporary .mp4 file
            var videoPath = Path.Combine(Path.GetTempPath(), $"video_{Guid.NewGuid()}.mp4");
            await File.WriteAllBytesAsync(videoPath, bytes);

            try
            {
                // Use your existing file-based method
                return await CreateVideoEmbeddingAsync(videoPath, frameRate);
            }
            finally
            {
                // Cleanup
                if (File.Exists(videoPath))
                    File.Delete(videoPath);
            }
        }

    }

}
