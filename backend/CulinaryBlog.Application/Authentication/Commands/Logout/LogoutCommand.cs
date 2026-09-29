using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands.Logout;

public sealed record LogoutCommand(string UserId, string? RefreshToken, bool AccessTokenExpired = false) : IRequest<bool>;

public sealed class LogoutCommandHandler(
    IRefreshTokenStore store,
    IRefreshTokenGenerator generator,
    IIdentityService identity,
    TimeProvider clock) : IRequestHandler<LogoutCommand, bool>
{
    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return false;

        if (request.AccessTokenExpired &&
            !(await identity.FindActiveUserAsync(request.UserId, cancellationToken)).Succeeded)
            return false;

        var revoked = await store.RevokeAsync(generator.Hash(request.RefreshToken), request.UserId,
            clock.GetUtcNow().UtcDateTime, request.AccessTokenExpired, cancellationToken);

        // With a valid access token, unknown/already-revoked/foreign tokens are indistinguishable.
        return !request.AccessTokenExpired || revoked;
    }
}
