using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands;

public sealed record GoogleLoginCommand(string? IdToken, string? AuthorizationCode)
    : IRequest<AuthResponseDto>;

public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => !string.IsNullOrWhiteSpace(command.IdToken) ||
                             !string.IsNullOrWhiteSpace(command.AuthorizationCode))
            .WithMessage("An ID token or authorization code is required.");
    }
}

public sealed class GoogleLoginCommandHandler(
    IGoogleCredentialVerifier verifier,
    IIdentityService identity,
    IJwtTokenService jwt,
    IRefreshTokenStore refreshTokens,
    IRefreshTokenGenerator generator,
    TimeProvider clock) : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(GoogleLoginCommand command, CancellationToken cancellationToken)
    {
        var google = await verifier.VerifyAsync(command.IdToken, command.AuthorizationCode, cancellationToken)
            ?? throw new UnauthorizedAccessException();
        var user = await identity.GoogleLoginAsync(
            google.Subject, google.Email, google.FullName, google.AvatarUrl, cancellationToken);
        if (!user.Succeeded)
            throw new UnauthorizedAccessException();

        var accessToken = await jwt.GenerateAccessTokenAsync(user);
        var refreshToken = generator.Generate();
        var now = clock.GetUtcNow().UtcDateTime;
        await refreshTokens.AddAsync(new CulinaryBlog.Domain.Entities.RefreshToken
        {
            Token = generator.Hash(refreshToken), UserId = user.UserId!, CreatedAt = now,
            UpdatedAt = now, ExpiresAt = now.AddDays(7)
        }, cancellationToken);

        return new AuthResponseDto(accessToken.Token, "Bearer", refreshToken,
            accessToken.ExpiresAt, AuthUserDto.FromIdentity(user));
    }
}

public interface IGoogleCredentialVerifier
{
    Task<GoogleIdentity?> VerifyAsync(string? idToken, string? authorizationCode,
        CancellationToken cancellationToken = default);
}

public sealed record GoogleIdentity(string Subject, string Email, string FullName, string? AvatarUrl);
