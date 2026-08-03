using BlogGraphQlApp.Services.Daily;
using System.Text.Json;

namespace BlogGraphQlApp.Endpoints
{
    public static class DailyWebhookEndpoint
    {
        /// <summary>
        /// Anonymous webhook receiver for Daily.co events. Daily posts here when
        /// participants join/leave or a room finishes. The request is only used to
        /// synchronise call state in the backend, never to mutate user data blindly:
        /// rooms are resolved against existing calls before anything changes.
        /// </summary>
        /// <param name="app">The endpoint route builder.</param>
        /// <returns>The configured endpoint route builder.</returns>
        public static IEndpointRouteBuilder MapDailyWebhook(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/daily/webhook", async (HttpContext http, DailyWebhookService webhook) =>
            {
                using var document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
                await webhook.HandleAsync(document.RootElement, http.RequestAborted);
                return Results.Ok();
            }).AllowAnonymous().DisableRateLimiting();

            return app;
        }
    }
}
