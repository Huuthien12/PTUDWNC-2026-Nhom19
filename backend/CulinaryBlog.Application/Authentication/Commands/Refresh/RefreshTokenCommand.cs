using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Authentication.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Authentication.Commands.Refresh;

public sealed record RefreshTokenCommand(string? RefreshToken) : IRequest<RefreshResult>;
public sealed record RefreshResult(AuthResponseDto? Tokens, string? ErrorCode);

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenStore store,
    IRefreshTokenGenerator generator,
    IIdentityService identity,
    IJwtTokenService jwt,
    TimeProvider clock,
    ILogger<RefreshTokenCommandHandler> logger) : IRequestHandler<RefreshTokenCommand, RefreshResult>
{
    public async Task<RefreshResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return new(null, "VALIDATION_ERROR");

        var token = await store.FindAsync(generator.Hash(request.RefreshToken), cancellationToken);
        if (token is null || token.IsDeleted)
            return new(null, "AUTH_TOKEN_INVALID");

        if (token.IsRevoked || token.ReplacedByToken is not null)
            return await ReusedAsync(token.UserId, token.Id, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        if (token.ExpiresAt <= now)
            return new(null, "AUTH_REFRESH_TOKEN_EXPIRED");

        var user = await identity.FindActiveUserAsync(token.UserId, cancellationToken);
        if (!user.Succeeded)
            return new(null, "AUTH_ACCOUNT_DISABLED");

        var accessToken = await jwt.GenerateAccessTokenAsync(user);
        var rawToken = generator.Generate();
        var replacement = new RefreshToken
        {
            Token = generator.Hash(rawToken),
            UserId = token.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddDays(7)
        };

        if (!await store.TryRotateAsync(token.Id, replacement, now, cancellationToken))
            return await ReusedAsync(token.UserId, token.Id, cancellationToken);

        return new(new(accessToken.Token, "Bearer", rawToken,
            accessToken.ExpiresAt, AuthUserDto.FromIdentity(user)), null);
    }

    private async Task<RefreshResult> ReusedAsync(
        string userId,
        Guid tokenId,
        CancellationToken cancellationToken)
    {
        await store.RevokeAllAsync(userId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        logger.LogWarning("Refresh token reuse or concurrent revocation detected for record {TokenId}.", tokenId);
        return new(null, "AUTH_REFRESH_TOKEN_REVOKED");
    }
}
