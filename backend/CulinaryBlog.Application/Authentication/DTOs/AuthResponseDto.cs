using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Application.Authentication.DTOs;

public sealed record AuthResponseDto(
    string AccessToken,
    string TokenType,
    string RefreshToken,
    DateTime ExpiresAt,
    AuthUserDto User);

public sealed record AuthUserDto(
    string Id,
    string FullName,
    string Email,
    string UserName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles)
{
    public static AuthUserDto FromIdentity(IdentityLoginResult identity) => new(
        identity.UserId!, identity.FullName!, identity.Email!, identity.UserName!,
        identity.AvatarUrl, identity.Roles);
}
