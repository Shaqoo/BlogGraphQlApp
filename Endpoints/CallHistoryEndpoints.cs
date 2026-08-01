using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.History;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlogGraphQlApp.Endpoints
{
    public static class CallHistoryEndpoints
    {
        /// <summary>
        /// Authenticated REST surface for the permanent call-history records.
        /// Supports pagination, date/status/type filtering and search by name.
        /// Deleting a record only removes the history entry; it never touches the
        /// (already temporary) Daily room.
        /// </summary>
        /// <param name="app">The route builder used to configure the call history endpoints.</param>
        /// <returns>The configured endpoint route builder.</returns>
        public static IEndpointRouteBuilder MapCallHistoryEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/call-history").RequireAuthorization();

            group.MapGet("/", async (
                ClaimsPrincipal user,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] CallHistoryStatus? status,
                [FromQuery] CallType? callType,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromQuery] string? search,
                [FromServices] ICallHistoryService service,
                CancellationToken ct) =>
            {
                var userId = user.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var query = new CallHistoryQuery(page ?? 1, pageSize ?? 20, status, callType, from, to, search);
                var result = await service.GetHistoryAsync(userId.Value, query, ct);
                return Results.Ok(result);
            });

            group.MapGet("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal user,
                [FromServices] ICallHistoryService service,
                CancellationToken ct) =>
            {
                var userId = user.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var item = await service.GetByIdAsync(userId.Value, id, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            });

            group.MapDelete("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal user,
                [FromServices] ICallHistoryService service,
                CancellationToken ct) =>
            {
                var userId = user.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var deleted = await service.DeleteAsync(userId.Value, id, ct);
                return deleted ? Results.Ok(new { Deleted = true }) : Results.NotFound();
            });

            group.MapDelete("/", async (
                ClaimsPrincipal user,
                [FromServices] ICallHistoryService service,
                CancellationToken ct) =>
            {
                var userId = user.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var count = await service.DeleteAllAsync(userId.Value, ct);
                return Results.Ok(new { DeletedCount = count });
            });

            return app;
        }

        private static Guid? GetUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
