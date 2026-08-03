namespace BlogGraphQlApp.Middleware
{
    /// <summary>
    /// Emits hardening headers on every response. Static files keep serving without
    /// Cache-Control: no-store, and HSTS is only sent over HTTPS.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var response = context.Response;

            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["X-Frame-Options"] = "DENY";
            response.Headers["Referrer-Policy"] = "no-referrer";
            response.Headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=()";
            response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            response.Headers["X-XSS-Protection"] = "0";

            var isHttps = context.Request.IsHttps ||
                          string.Equals(context.Request.Headers["X-Forwarded-Proto"],
                              "https", StringComparison.OrdinalIgnoreCase);
            if (isHttps)
                response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            var path = context.Request.Path.Value ?? string.Empty;
            var isApi = path.StartsWith("/api/", StringComparison.Ordinal) ||
                        path.Equals("/gql", StringComparison.Ordinal) ||
                        path.StartsWith("/gql/", StringComparison.Ordinal);
            if (isApi && !response.Headers.ContainsKey("Cache-Control"))
                response.Headers["Cache-Control"] = "no-store";

            await _next(context);
        }
    }
}
