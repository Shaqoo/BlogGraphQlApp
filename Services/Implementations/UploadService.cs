using BlogGraphQlApp.Core.Interfaces;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class UploadService : IUploadService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UploadService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> UploadFileAsync(IFile file, string subfolder)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is empty or null.", nameof(file));
            }

            var uploadsFolder = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subfolder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + System.IO.Path.GetExtension(file.Name);
            var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{subfolder}/{uniqueFileName}";
        }

        public Task<bool> DeleteFileAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return Task.FromResult(true);

            var filePath = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, fileUrl.TrimStart('/'));

            if (!File.Exists(filePath)) return Task.FromResult(false);

            File.Delete(filePath);
            return Task.FromResult(true);
        }

        public Task<string> UploadAvatarAsync(byte[] data,string initials)
        {
            var folderPath = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "avatars");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{initials}_{Guid.NewGuid()}.png";
            var filePath = System.IO.Path.Combine(folderPath, fileName);

            System.IO.File.WriteAllBytes(filePath, data);

            return Task.FromResult($"/avatars/{fileName}");
        }
    }
}