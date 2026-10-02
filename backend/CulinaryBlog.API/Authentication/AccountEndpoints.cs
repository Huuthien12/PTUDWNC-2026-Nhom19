using System.Security.Claims;
using CulinaryBlog.Application.Authentication.Commands;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CulinaryBlog.API.Authentication;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapPost("/google", async (GoogleLoginRequest request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GoogleLoginCommand(request.IdToken, request.AuthorizationCode), cancellationToken)));

        auth.MapGet("/me", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetCurrentUserQuery(UserId(user)), cancellationToken)))
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        auth.MapPatch("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, ISender sender,
            CancellationToken cancellationToken) => Results.Ok(await sender.Send(
                new UpdateProfileCommand(UserId(user), request.FullName, request.AvatarUrl), cancellationToken)))
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();
}

public sealed record GoogleLoginRequest(string? IdToken, string? AuthorizationCode);
public sealed record UpdateProfileRequest(string? FullName, string? AvatarUrl);
