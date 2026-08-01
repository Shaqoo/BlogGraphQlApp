using BlogGraphQlApp.Storage;
using HotChocolate.Execution;

namespace BlogGraphQlApp.GraphQL.Errors
{
    /// <summary>
    /// Global exception handler for the GraphQL pipeline. Converts every exception
    /// that escapes a resolver or mutation into a client-safe GraphQL error.
    ///
    /// - <see cref="GraphQLException"/> keeps its intended message (already client-facing).
    /// - <see cref="InvalidFileException"/> surfaces the validation message as a
    ///   VALIDATION_ERROR (bad extension, MIME type, or file size).
    /// - Any other exception is logged in full and reported as a generic
    ///   INTERNAL_SERVER_ERROR, so internal details never leak to clients.
    /// </summary>
    public class GraphQLErrorFilter : IErrorFilter
    {
        private readonly ILogger<GraphQLErrorFilter> _logger;

        public GraphQLErrorFilter(ILogger<GraphQLErrorFilter> logger)
        {
            _logger = logger;
        }

        public IError OnError(IError error)
        {
            var exception = error.Exception;
            if (exception is null)
                return error;

            return exception switch
            {
                GraphQLException => StripError(error, exception.Message, exception.Message),
                InvalidFileException => StripError(error, "VALIDATION_ERROR", exception.Message),
                _ => HandleUnexpectedError(error, exception)
            };
        }

        private IError HandleUnexpectedError(IError error, Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception during GraphQL request execution");
            return StripError(error, "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred. Please try again later.");
        }

        private static IError StripError(IError error, string code, string message) =>
            ErrorBuilder.FromError(error)
                .SetCode(code)
                .SetMessage(message)
                .SetException(null)
                .Build();
    }
}
