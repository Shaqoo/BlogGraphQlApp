using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace BlogGraphQlApp.GraphQL.Subscriptions;

/// <summary>
/// Authenticates GraphQL WebSocket subscriptions. Browsers and mobile clients
/// cannot set HTTP headers on a WebSocket handshake, so the JWT is expected in
/// the <c>connection_init</c> payload (for example <c>{ Authorization: "Bearer &lt;token&gt;" }</c>).
/// The Authorization header from the initial upgrade request is used as a fallback.
/// </summary>
public class SocketSessionInterceptor : DefaultSocketSessionInterceptor
{
    private readonly TokenValidationParameters _validationParameters;

    public SocketSessionInterceptor(IConfiguration configuration)
    {
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["Jwt:Key"]!))
        };
    }

    public override async ValueTask<ConnectionStatus> OnConnectAsync(
        ISocketSession session,
        IOperationMessagePayload connectionInitMessage,
        CancellationToken cancellationToken)
    {
        var status = await base.OnConnectAsync(session, connectionInitMessage, cancellationToken);
        if (!status.Accepted)
        {
            return status;
        }

        var token = ExtractToken(session, connectionInitMessage);
        if (string.IsNullOrWhiteSpace(token))
        {
            return status;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var tokenPrincipal = handler.ValidateToken(token, _validationParameters, out _);
            var identity = new ClaimsIdentity(tokenPrincipal.Claims, "Bearer");
            session.Connection.HttpContext.User = new ClaimsPrincipal(identity);
        }
        catch (Exception)
        {
            return ConnectionStatus.Reject("Invalid authentication token.");
        }

        return ConnectionStatus.Accept();
    }

    private static string? ExtractToken(ISocketSession session, IOperationMessagePayload message)
    {
        if (message.Payload is { } payload && payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "Authorization", "authorization", "authToken", "access_token" })
            {
                if (payload.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    return StripBearer(value);
                }
            }
        }

        return StripBearer(session.Connection.HttpContext.Request.Headers.Authorization.ToString());
    }

    private static string? StripBearer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value["Bearer ".Length..].Trim()
            : value.Trim();
    }
}
