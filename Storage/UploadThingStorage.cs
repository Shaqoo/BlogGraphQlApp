using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Path = System.IO.Path;

namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Uploads files to UploadThing (https://uploadthing.com) and returns their public URLs.
    /// Used in the Production environment.
    ///
    /// Authentication uses the app's API key (the "Secret", e.g. <c>sk_live_...</c>) sent in
    /// the <c>x-uploadthing-api-key</c> header. The key is read from configuration only:
    /// <c>UPLOADTHING_SECRET</c> (environment variable) or <c>UploadThing:Secret</c> (appsettings).
    /// It is never hardcoded and never derived from a token.
    ///
    /// Large files are streamed straight to UploadThing's presigned URL; the file bytes are never
    /// buffered into server memory. Transient API failures are retried with exponential backoff.
    /// </summary>
    public class UploadThingStorage : IFileStorage
    {
        /// <summary>Name of the typed HttpClient registered for UploadThing calls.</summary>
        public const string HttpClientName = "UploadThing";

        private const string ApiBaseUrl = "https://api.uploadthing.com";
        private const string PrepareUploadEndpoint = "/v7/prepareUpload";
        private const string DeleteFilesEndpoint = "/v6/deleteFiles";
        private const string UploadthingVersion = "7.7.4";
        private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(10);

        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<StorageValidationOptions> _validationOptions;
        private readonly ILogger<UploadThingStorage> _logger;
        private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

        public UploadThingStorage(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IOptions<StorageValidationOptions> validationOptions,
            ILogger<UploadThingStorage> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _validationOptions = validationOptions;
            _logger = logger;

            _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(500),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<TaskCanceledException>()
                        .Handle<HttpRequestException>(static ex =>
                            ex.StatusCode is null ||
                            ex.StatusCode == HttpStatusCode.TooManyRequests ||
                            (int)ex.StatusCode >= 500)
                })
                .Build();
        }

        public async Task<string> UploadAsync(IFile file, string subfolder)
        {
            if (file is null)
                throw new InvalidFileException("No file was provided.");

            if (file.Length <= 0)
                throw new InvalidFileException("The uploaded file is empty.");

            FileValidator.Validate(file, _validationOptions.Value);

            var fileName = Path.GetFileName(file.Name);
            try
            {
                // OpenReadStream is callable multiple times; each retry re-opens a fresh stream
                // so the request body never needs to be buffered in memory.
                return await UploadFileAsync(() => file.OpenReadStream(), fileName, file.ContentType, file.Length ?? 0, subfolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadThing upload failed for {FileName}", fileName);
                throw;
            }
        }

        public async Task<string> UploadAsync(byte[] data, string subfolder, string fileName)
        {
            if (data is null || data.Length == 0)
                throw new InvalidFileException("File content is empty.");

            var safeFileName = Path.GetFileName(fileName);
            var contentType = FileValidator.GuessContentType(safeFileName);

            FileValidator.Validate(safeFileName, contentType, data.Length, _validationOptions.Value);

            try
            {
                return await UploadFileAsync(
                    () => new MemoryStream(data, writable: false),
                    safeFileName,
                    contentType,
                    data.Length,
                    subfolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadThing upload failed for {FileName}", safeFileName);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string? fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return true;

            var fileKey = ExtractFileKey(fileUrl);
            if (fileKey is null)
            {
                _logger.LogWarning("Could not extract an UploadThing file key from '{FileUrl}'; skipping delete.", fileUrl);
                return true;
            }

            try
            {
                using var response = await SendWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + DeleteFilesEndpoint);
                    request.Headers.Add("x-uploadthing-api-key", GetApiKey());
                    request.Content = JsonContent.Create(new { fileKeys = new[] { fileKey } });
                    return request;
                });

                var json = await ReadJsonAsync(response);
                var success = json.TryGetProperty("success", out var successProperty) && successProperty.GetBoolean();

                _logger.LogInformation("UploadThing delete completed {FileKey} (success: {Success})", fileKey, success);
                return success;
            }
            catch (Exception ex)
            {
                // Deleting an already-removed, unreachable or invalid file must not break the caller.
                _logger.LogWarning(ex, "UploadThing delete failed for {FileKey}", fileKey);
                return false;
            }
        }

        public async Task<byte[]> DownloadAsync(string? fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new ArgumentException("File URL is empty or null.", nameof(fileUrl));

            if (!fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"File '{fileUrl}' is not a remote UploadThing URL and cannot be downloaded in production.");
            }

            using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, fileUrl));
            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<string> UploadFileAsync(
            Func<Stream> openStream,
            string fileName,
            string? contentType,
            long fileSize,
            string subfolder)
        {
            var apiKey = GetApiKey();
            var (presignedUrl, fileKey) = await PrepareUploadAsync(apiKey, fileName, fileSize, contentType);

            _logger.LogInformation(
                "UploadThing upload started {FileName} ({FileSize} bytes, folder {Subfolder})",
                fileName, fileSize, subfolder);

            using var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl);
                request.Headers.Add("Range", "bytes=0-");
                request.Headers.Add("x-uploadthing-version", UploadthingVersion);

                var formData = new MultipartFormDataContent();
                formData.Add(new StreamContent(openStream()), "file", fileName);
                request.Content = formData;
                return request;
            });

            var json = await ReadJsonAsync(response);

            var fileUrl = TryGetString(json, "ufsUrl") ?? TryGetString(json, "url");
            if (string.IsNullOrEmpty(fileUrl))
            {
                var appId = GetAppId();
                if (!string.IsNullOrEmpty(appId))
                    fileUrl = $"https://{appId}.ufs.sh/f/{fileKey}";
            }

            if (string.IsNullOrEmpty(fileUrl))
                throw new InvalidOperationException("UploadThing did not return a file URL after upload.");

            _logger.LogInformation(
                "UploadThing upload completed {FileKey} as {FileUrl} ({FileSize} bytes)",
                fileKey, fileUrl, fileSize);

            return fileUrl;
        }

        private async Task<(string Url, string Key)> PrepareUploadAsync(string apiKey, string fileName, long fileSize, string? contentType)
        {
            using var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + PrepareUploadEndpoint);
                request.Headers.Add("x-uploadthing-api-key", apiKey);
                request.Content = JsonContent.Create(new
                {
                    fileName,
                    fileSize,
                    fileType = string.IsNullOrWhiteSpace(contentType) ? null : contentType,
                    contentDisposition = "inline",
                    acl = "public-read"
                });
                return request;
            });

            var json = await ReadJsonAsync(response);

            var url = TryGetString(json, "url")
                ?? throw new InvalidOperationException("UploadThing did not return an upload URL.");
            var key = TryGetString(json, "key")
                ?? throw new InvalidOperationException("UploadThing did not return a file key.");

            return (url, key);
        }

        /// <summary>
        /// Sends an HTTP request through the Polly retry pipeline. Non-success responses are
        /// converted to <see cref="HttpRequestException"/> carrying the status code; the pipeline
        /// retries only transient failures (timeouts, 429, 5xx).
        /// </summary>
        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory)
        {
            return await _retryPipeline.ExecuteAsync(async ct =>
            {
                using var request = requestFactory();

                var client = _httpClientFactory.CreateClient(HttpClientName);
                client.Timeout = UploadTimeout;

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    var status = response.StatusCode;
                    response.Dispose();
                    throw new HttpRequestException(
                        $"UploadThing request failed ({(int)status}). Response: {errorBody}",
                        null,
                        status);
                }

                return response;
            });
        }

        private string GetApiKey()
        {
            var secret = _configuration["UPLOADTHING_SECRET"] ?? _configuration["UploadThing:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    "UploadThing storage is enabled but the UploadThing API key is missing. " +
                    "Set the UPLOADTHING_SECRET environment variable (or UploadThing:Secret in app settings) " +
                    "when running in the Production environment.");

            return secret;
        }

        private string? GetAppId() =>
            _configuration["UPLOADTHING_APP_ID"] ?? _configuration["UploadThing:AppId"];

        private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }

        private static string? ExtractFileKey(string fileUrl)
        {
            var value = fileUrl.Trim();

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                var slash = value.LastIndexOf('/');
                var candidate = slash >= 0 ? value[(slash + 1)..] : value;
                return string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim();
            }

            var segments = uri.Segments;
            if (segments.Length >= 2 &&
                segments[segments.Length - 2].TrimEnd('/').Equals("f", StringComparison.OrdinalIgnoreCase))
            {
                return segments[segments.Length - 1].Trim('/');
            }

            var last = segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrWhiteSpace(last) ? null : last;
        }

        private static string? TryGetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }
}
