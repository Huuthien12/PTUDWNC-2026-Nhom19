using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CulinaryBlog.Application.Authentication.Commands.Logout;
using CulinaryBlog.Application.Authentication.Commands.Refresh;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.API.Authentication;

public static class TokenEndpoints
{
    public static void MapTokenEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapPost("/refresh", async (TokenRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
            if (result.Tokens is not null)
                return Results.Ok(result.Tokens);
            return Results.Problem(type: result.ErrorCode, title: "Token refresh failed.",
                statusCode: result.ErrorCode == "VALIDATION_ERROR" ? 400 : 401);
        });

        // FR-AUTH-005 A2: an expired, otherwise valid JWT also needs its owner's active refresh token.
        auth.MapPost("/logout", async (TokenRequest request, HttpContext context, ISender sender,
            IOptionsMonitor<JwtBearerOptions> options, CancellationToken cancellationToken) =>
        {
            var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var principal = authentication.Principal;
            var expired = false;
            if (!authentication.Succeeded)
            {
                if (authentication.Failure is not SecurityTokenExpiredException)
                    return Unauthorized();

                principal = ValidateExpiredToken(context, options.Get(JwtBearerDefaults.AuthenticationScheme));
                if (principal is null)
                    return Unauthorized();
                expired = true;
            }

            var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return expired ? Unauthorized() : Results.Problem(type: "VALIDATION_ERROR",
                    title: "Refresh token is required.", statusCode: 400);

            var success = await sender.Send(new LogoutCommand(userId, request.RefreshToken, expired), cancellationToken);
            return success ? Results.NoContent() : Unauthorized();
        });
    }

    private static IResult Unauthorized() => Results.Problem(type: "AUTH_TOKEN_INVALID",
        title: "A valid access token or an expired access token with an active refresh token is required.", statusCode: 401);

    private static ClaimsPrincipal? ValidateExpiredToken(HttpContext context, JwtBearerOptions options)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var parameters = options.TokenValidationParameters.Clone();
        // Reuse the middleware's signature/issuer/audience settings. Only tolerate past exp.
        parameters.LifetimeValidator = (notBefore, expires, _, _) =>
            expires.HasValue && expires.Value <= DateTime.UtcNow &&
            (!notBefore.HasValue || (notBefore.Value <= DateTime.UtcNow && notBefore.Value <= expires.Value));
        try
        {
            return new JwtSecurityTokenHandler { MapInboundClaims = options.MapInboundClaims }
                .ValidateToken(header[7..].Trim(), parameters, out _);
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
    }
}

public sealed record TokenRequest(string? RefreshToken);
