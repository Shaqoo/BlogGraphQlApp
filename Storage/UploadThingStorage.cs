using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Path = System.IO.Path;

namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Uploads files to UploadThing (https://uploadthing.com) and returns their public URLs.
    /// Used in the Production environment. Requests are authenticated with the app's API key
    /// (the "Secret", e.g. <c>sk_live_...</c>). It is read from configuration:
    /// <c>UPLOADTHING_SECRET</c> / <c>UploadThing:Secret</c>, or derived from the
    /// <c>UPLOADTHING_TOKEN</c> / <c>UploadThing:Token</c> value.
    /// </summary>
    public class UploadThingStorage : IFileStorage
    {
        private const string ApiBaseUrl = "https://api.uploadthing.com";
        private const string PrepareUploadEndpoint = "/v7/prepareUpload";
        private const string DeleteFilesEndpoint = "/v6/deleteFiles";
        private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(10);

        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<UploadThingStorage> _logger;

        public UploadThingStorage(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<UploadThingStorage> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> UploadAsync(IFile file, string subfolder)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null.", nameof(file));

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            return await UploadBytesAsync(memoryStream.ToArray(), file.Name, file.ContentType);
        }

        public Task<string> UploadAsync(byte[] data, string subfolder, string fileName)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("File content is empty or null.", nameof(data));

            var contentType = GuessContentType(fileName);
            return UploadBytesAsync(data, fileName, contentType);
        }

        public async Task<bool> DeleteAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return true;

            var fileKey = ExtractFileKey(fileUrl);
            if (fileKey is null)
            {
                _logger.LogWarning("Skipping UploadThing delete for '{FileUrl}': could not extract a file key.", fileUrl);
                return false;
            }

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + DeleteFilesEndpoint);
            request.Headers.Add("x-uploadthing-api-key", GetApiKey());
            request.Content = JsonContent.Create(new { fileKeys = new[] { fileKey } });

            using var response = await client.SendAsync(request);
            var json = await ReadJsonOrThrowAsync(response, "delete file");

            var success = json.TryGetProperty("success", out var successProp)
                ? successProp.GetBoolean()
                : false;

            _logger.LogInformation("Deleted UploadThing file {Key} (success: {Success})", fileKey, success);
            return success;
        }

        public async Task<byte[]> DownloadAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                throw new ArgumentException("File URL is empty or null.", nameof(fileUrl));

            if (!fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"File '{fileUrl}' is not a remote UploadThing URL and cannot be downloaded in production.");
            }

            var client = _httpClientFactory.CreateClient();
            return await client.GetByteArrayAsync(fileUrl);
        }

        private async Task<string> UploadBytesAsync(byte[] data, string fileName, string contentType)
        {
            var apiKey = GetApiKey();

            var prepareResponse = await RequestPresignedUploadAsync(apiKey, fileName, data.Length, contentType);
            var presignedUrl = prepareResponse.GetProperty("url").GetString()
                ?? throw new InvalidOperationException("UploadThing did not return an upload URL.");
            var fileKey = prepareResponse.GetProperty("key").GetString()
                ?? throw new InvalidOperationException("UploadThing did not return a file key.");

            _logger.LogInformation("Uploading file {Name} ({Size} bytes) to UploadThing", fileName, data.Length);

            using var putRequest = new HttpRequestMessage(HttpMethod.Put, presignedUrl);
            putRequest.Headers.Add("Range", "bytes=0-");

            using var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(data), "file", fileName);
            putRequest.Content = formData;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = UploadTimeout;

            using var uploadResponse = await client.SendAsync(putRequest);
            var uploadJson = await ReadJsonOrThrowAsync(uploadResponse, "upload file");

            var fileUrl = TryGetString(uploadJson, "ufsUrl") ?? TryGetString(uploadJson, "url");
            if (string.IsNullOrEmpty(fileUrl))
            {
                // Fallback: build the canonical public URL from the app id if the
                // upload response did not include a URL.
                var appId = GetAppId();
                if (!string.IsNullOrEmpty(appId))
                    fileUrl = $"https://{appId}.ufs.sh/f/{fileKey}";
            }

            if (string.IsNullOrEmpty(fileUrl))
                throw new InvalidOperationException("UploadThing did not return a file URL after upload.");

            _logger.LogInformation("Uploaded file {Key} to UploadThing as {Url}", fileKey, fileUrl);
            return fileUrl;
        }

        private async Task<JsonElement> RequestPresignedUploadAsync(string apiKey, string fileName, int fileSize, string fileType)
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + PrepareUploadEndpoint);
            request.Headers.Add("x-uploadthing-api-key", apiKey);
            request.Content = JsonContent.Create(new
            {
                fileName,
                fileSize,
                fileType,
                contentDisposition = "inline",
                acl = "public-read"
            });

            using var response = await client.SendAsync(request);
            return await ReadJsonOrThrowAsync(response, "prepare upload");
        }

        private string GetApiKey()
        {
            // UploadThing authenticates requests with the API key ("Secret"), e.g. "sk_live_...",
            // sent in the x-uploadthing-api-key header.
            var secret = _configuration["UPLOADTHING_SECRET"] ?? _configuration["UploadThing:Secret"];
            if (!string.IsNullOrWhiteSpace(secret))
                return secret;

            // Fallback: derive the API key from the full token (base64 JSON with apiKey/appId/regions).
            var token = _configuration["UPLOADTHING_TOKEN"] ?? _configuration["UploadThing:Token"];
            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                    using var document = JsonDocument.Parse(decoded);
                    var root = document.RootElement;

                    foreach (var propertyName in new[] { "apiKey", "key" })
                    {
                        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                            return value.GetString()!;
                    }
                }
                catch (Exception)
                {
                    _logger.LogWarning("UPLOADTHING_TOKEN could not be decoded to an API key.");
                }
            }

            throw new InvalidOperationException(
                "UploadThing API key is missing. Set the UPLOADTHING_SECRET environment variable " +
                "(or UploadThing:Secret in app settings) when using UploadThing storage.");
        }

        private string? GetAppId() =>
            _configuration["UPLOADTHING_APP_ID"] ?? _configuration["UploadThing:AppId"];

        private static async Task<JsonElement> ReadJsonOrThrowAsync(HttpResponseMessage response, string action)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"UploadThing failed to {action} ({(int)response.StatusCode}). Response: {content}");
            }

            using var document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }

        private static string? ExtractFileKey(string fileUrl)
        {
            if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
                return null;

            var segment = uri.Segments.LastOrDefault();
            return string.IsNullOrEmpty(segment) ? null : segment.Trim('/');
        }

        private static string? TryGetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

        private static string GuessContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}
