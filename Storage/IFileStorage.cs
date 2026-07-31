namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Abstraction over where uploaded files are stored. The rest of the application
    /// depends on this interface instead of reaching into wwwroot or an external provider.
    /// </summary>
    public interface IFileStorage
    {
        /// <summary>Uploads an uploaded file and returns the publicly accessible URL.</summary>
        /// <param name="file">The uploaded file to persist.</param>
        /// <param name="subfolder">Logical folder the file belongs to (e.g. "images", "videos").</param>
        Task<string> UploadAsync(IFile file, string subfolder);

        /// <summary>Uploads raw bytes and returns the publicly accessible URL.</summary>
        /// <param name="data">The bytes to persist.</param>
        /// <param name="subfolder">Logical folder the file belongs to.</param>
        /// <param name="fileName">The name (including extension) to store the file under.</param>
        Task<string> UploadAsync(byte[] data, string subfolder, string fileName);

        /// <summary>Deletes the file referenced by a previously returned URL.</summary>
        /// <returns>True when the file was deleted or there was nothing to delete.</returns>
        Task<bool> DeleteAsync(string? fileUrl);

        /// <summary>Downloads the content of a previously uploaded file as bytes.</summary>
        Task<byte[]> DownloadAsync(string? fileUrl);
    }
}
