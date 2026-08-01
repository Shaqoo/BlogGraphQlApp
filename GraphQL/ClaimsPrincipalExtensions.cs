using System.Security.Claims;
using HotChocolate.AspNetCore;

namespace BlogGraphQlApp.GraphQL
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal claimsPrincipal)
        {
            var userIdString = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            return !Guid.TryParse(userIdString, out var userId)
                ? throw new GraphQLRequestException("User not authenticated.")
                : userId;
        }
    }
}
