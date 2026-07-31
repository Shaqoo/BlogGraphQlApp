using Microsoft.AspNetCore.Http;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IUploadService
    {
        Task<string> UploadFileAsync(IFile file, string subfolder);
        Task<string> UploadAvatarAsync(byte[] data,string initials);
        Task<bool> DeleteFileAsync(string? fileUrl);
    }
}