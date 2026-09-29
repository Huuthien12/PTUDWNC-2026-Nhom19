using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IRefreshTokenGenerator _generator;
    private readonly TimeProvider _clock;

    public RegisterCommandHandler(
        IIdentityService identity,
        IJwtTokenService jwt,
        IRefreshTokenStore refreshTokens,
        IRefreshTokenGenerator generator,
        TimeProvider clock)
    {
        _identity = identity;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _generator = generator;
        _clock = clock;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var registered = await _identity.RegisterAsync(
            request.FullName.Trim(),
            request.Email.Trim(),
            request.UserName.Trim(),
            request.Password,
            cancellationToken);
        if (!registered.Succeeded)
            return new(null, registered.ErrorCode);

        var user = registered.User!;
        var accessToken = await _jwt.GenerateAccessTokenAsync(user);
        var refreshToken = _generator.Generate();
        var now = _clock.GetUtcNow().UtcDateTime;
        await _refreshTokens.AddAsync(new RefreshToken
        {
            Token = _generator.Hash(refreshToken),
            UserId = user.UserId!,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddDays(7)
        }, cancellationToken);

        return new(new AuthResponseDto(
            accessToken.Token,
            "Bearer",
            refreshToken,
            accessToken.ExpiresAt,
            AuthUserDto.FromIdentity(user)), null);
    }
}
