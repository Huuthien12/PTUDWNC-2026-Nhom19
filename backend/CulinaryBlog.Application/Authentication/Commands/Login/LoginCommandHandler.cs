using CulinaryBlog.Application.Common.Interfaces;
using MediatR;
using CulinaryBlog.Application.Authentication.DTOs;

namespace CulinaryBlog.Application.Authentication.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IRefreshTokenGenerator _generator;
    private readonly TimeProvider _clock;

    public LoginCommandHandler(
        IIdentityService identityService,
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore refreshTokens,
        IRefreshTokenGenerator generator,
        TimeProvider clock)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
        _refreshTokens = refreshTokens;
        _generator = generator;
        _clock = clock;
    }

    public async Task<LoginResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var identityResult =
            await _identityService.LoginAsync(
                request.Email.Trim(),
                request.Password,
                cancellationToken);

        if (!identityResult.Succeeded)
        {
            return new LoginResult(
                false,
                null,
                "Bearer",
                identityResult.ErrorCode
                    ?? "INVALID_CREDENTIALS");
        }

        var accessToken =
            await _jwtTokenService.GenerateAccessTokenAsync(
                identityResult);

        var refreshToken = _generator.Generate();
        var now = _clock.GetUtcNow().UtcDateTime;
        await _refreshTokens.AddAsync(new CulinaryBlog.Domain.Entities.RefreshToken
        {
            Token = _generator.Hash(refreshToken),
            UserId = identityResult.UserId!,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddDays(7)
        }, cancellationToken);

        return new LoginResult(
            true,
            accessToken.Token,
            "Bearer",
            null,
            refreshToken,
            accessToken.ExpiresAt,
            AuthUserDto.FromIdentity(identityResult));
    }
}
