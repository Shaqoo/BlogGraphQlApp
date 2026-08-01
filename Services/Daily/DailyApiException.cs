namespace BlogGraphQlApp.Services.Daily
{
    /// <summary>
    /// Raised when the Daily REST API returns a non-success response or cannot be reached.
    /// The message is safe to surface to callers.
    /// </summary>
    public class DailyApiException : Exception
    {
        public int StatusCode { get; }

        public DailyApiException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        public DailyApiException(string message) : base(message)
        {
            StatusCode = 0;
        }
    }
}
