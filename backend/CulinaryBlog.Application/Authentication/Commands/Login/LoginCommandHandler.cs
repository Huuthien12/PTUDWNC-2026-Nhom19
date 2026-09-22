using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandHandler(
        IIdentityService identityService,
        IJwtTokenService jwtTokenService)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
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

        return new LoginResult(
            true,
            accessToken,
            "Bearer",
            null);
    }
}
