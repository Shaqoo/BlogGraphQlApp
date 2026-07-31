using System.IO;
using Path = System.IO.Path;

namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Stores files under the <c>wwwroot/uploads</c> folder and returns local URLs.
    /// Used in the Development environment so files continue to be served by static files.
    /// </summary>
    public class LocalFileStorage : IFileStorage
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LocalFileStorage> _logger;

        public LocalFileStorage(
            IWebHostEnvironment environment,
            IHttpClientFactory httpClientFactory,
            ILogger<LocalFileStorage> logger)
        {
            _environment = environment;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> UploadAsync(IFile file, string subfolder)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null.", nameof(file));

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.Name)}";
            var relativeUrl = $"uploads/{subfolder}/{fileName}";
            var fullPath = ResolvePhysicalPath(relativeUrl);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using (var fileStream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/" + relativeUrl;
        }

        public async Task<string> UploadAsync(byte[] data, string subfolder, string fileName)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("File content is empty or null.", nameof(data));

            var safeFileName = Path.GetFileName(fileName);
            var relativeUrl = $"uploads/{subfolder}/{safeFileName}";
            var fullPath = ResolvePhysicalPath(relativeUrl);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, data);

            return "/" + relativeUrl;
        }

        public Task<bool> DeleteAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return Task.FromResult(true);

            var fullPath = ResolvePhysicalPath(fileUrl.TrimStart('/'));

            if (!File.Exists(fullPath))
                return Task.FromResult(false);

            File.Delete(fullPath);
            _logger.LogInformation("Deleted local file {Path}", fullPath);
            return Task.FromResult(true);
        }

        public async Task<byte[]> DownloadAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                throw new ArgumentException("File URL is empty or null.", nameof(fileUrl));

            if (IsRemoteUrl(fileUrl))
            {
                var client = _httpClientFactory.CreateClient();
                return await client.GetByteArrayAsync(fileUrl);
            }

            var fullPath = ResolvePhysicalPath(fileUrl.TrimStart('/'));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("File not found.", fullPath);

            return await File.ReadAllBytesAsync(fullPath);
        }

        private static bool IsRemoteUrl(string fileUrl) =>
            fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private string ResolvePhysicalPath(string relativeUrl)
        {
            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrEmpty(webRootPath))
            {
                // wwwroot is git-ignored, so it may be missing on a fresh clone.
                // Fall back to the content root so uploads still work in Development.
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            Directory.CreateDirectory(webRootPath);

            var fullPath = Path.GetFullPath(Path.Combine(webRootPath, relativeUrl));
            var webRootFullPath = Path.GetFullPath(webRootPath);

            if (!fullPath.StartsWith(webRootFullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Path '{relativeUrl}' escapes the web root.");

            return fullPath;
        }
    }
}
