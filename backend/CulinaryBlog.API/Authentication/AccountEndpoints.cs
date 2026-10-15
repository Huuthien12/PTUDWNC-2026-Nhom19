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
            Results.Ok(await sender.Send(new GoogleLoginCommand(request.IdToken, request.AuthorizationCode, request.CodeVerifier), cancellationToken)));

        auth.MapGet("/me", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetCurrentUserQuery(UserId(user)), cancellationToken)))
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        auth.MapPatch("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, ISender sender,
            CancellationToken cancellationToken) => Results.Ok(await sender.Send(
                new UpdateProfileCommand(UserId(user), request.FullName, request.AvatarUrl), cancellationToken)))
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user, ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ChangePasswordCommand(UserId(user), request.CurrentPassword,
                request.NewPassword, request.ConfirmNewPassword), cancellationToken);
            if (result.Succeeded) return Results.NoContent();
            if (result.ErrorCode == "AUTH_ACCOUNT_LOCKED")
                return Results.Problem(type: result.ErrorCode, title: "Account locked.",
                    statusCode: StatusCodes.Status423Locked,
                    extensions: new Dictionary<string, object?> { ["lockedUntil"] = result.LockoutEnd });
            if (result.ErrorCode == "AUTH_ACCOUNT_DISABLED") return Results.Unauthorized();
            return Results.Problem(type: result.ErrorCode ?? "VALIDATION_ERROR", title: "Change password failed.",
                statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser());
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();
}

public sealed record GoogleLoginRequest(string? IdToken, string? AuthorizationCode, string? CodeVerifier);
public sealed record UpdateProfileRequest(string? FullName, string? AvatarUrl);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
