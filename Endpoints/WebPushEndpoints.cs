using BlogGraphQlApp.Config;
using Microsoft.Extensions.Options;

namespace BlogGraphQlApp.Endpoints
{
    public static class WebPushEndpoints
    {
        /// <summary>
        /// Anonymous endpoint that exposes only the VAPID public key. The public key is
        /// safe to share with any client: it is the <c>applicationServerKey</c> the
        /// browser must pass to <c>PushManager.subscribe</c>. The private key is never
        /// exposed and never leaves the server.
        /// </summary>
        public static IEndpointRouteBuilder MapWebPushEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/web-push/vapid-key", (IOptions<VapidSettings> options) =>
            {
                var publicKey = options.Value.PublicKey;
                if (string.IsNullOrWhiteSpace(publicKey))
                    return Results.Problem("Web Push is not configured.", statusCode: 503);

                return Results.Ok(new { publicKey });
            }).AllowAnonymous();

            return app;
        }
    }
}
