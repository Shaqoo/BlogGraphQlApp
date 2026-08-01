namespace BlogGraphQlApp.Storage
{
    /// <summary>
    /// Thrown when an upload is rejected by validation (bad extension, MIME type,
    /// or file size). The message is surfaced to GraphQL clients as a mutation error.
    /// </summary>
    public class InvalidFileException : Exception
    {
        public InvalidFileException(string message) : base(message) { }
    }
}
