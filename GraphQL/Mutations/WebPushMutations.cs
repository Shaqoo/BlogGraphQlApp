using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Services.Push;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class WebPushMutations
    {
        [Authorize]
        [GraphQLDescription("Registers the browser web-push subscription of the current user.")]
        public async Task<ApiResponse<bool>> RegisterPushSubscriptionAsync(
            string endpoint,
            string p256dh,
            string auth,
            ClaimsPrincipal claimsPrincipal,
            [Service] IWebPushService webPushService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            await webPushService.RegisterAsync(userId, endpoint, p256dh, auth, cancellationToken);
            return ApiResponse<bool>.Success(true, "Push subscription registered.");
        }

        [Authorize]
        [GraphQLDescription("Removes the given web-push subscription of the current user.")]
        public async Task<ApiResponse<bool>> UnregisterPushSubscriptionAsync(
            string endpoint,
            ClaimsPrincipal claimsPrincipal,
            [Service] IWebPushService webPushService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            await webPushService.RemoveAsync(userId, endpoint, cancellationToken);
            return ApiResponse<bool>.Success(true, "Push subscription removed.");
        }
    }
}
