namespace CulinaryBlog.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<IdentityRegistrationResult> RegisterAsync(
        string fullName,
        string email,
        string userName,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityLoginResult> FindActiveUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<IdentityLoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityLoginResult> GoogleLoginAsync(
        string providerKey,
        string email,
        string fullName,
        string? avatarUrl,
        CancellationToken cancellationToken = default);

    Task<UserProfileResult?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserProfileResult?> UpdateProfileAsync(
        string userId,
        string? fullName,
        string? avatarUrl,
        CancellationToken cancellationToken = default);
}

public sealed record IdentityLoginResult(
    bool Succeeded,
    string? UserId,
    string? Email,
    string? FullName,
    IReadOnlyList<string> Roles,
    string? ErrorCode,
    string? UserName = null,
    string? AvatarUrl = null);

public sealed record IdentityRegistrationResult(
    bool Succeeded,
    IdentityLoginResult? User,
    string? ErrorCode);

public sealed record UserProfileResult(
    string Id,
    string FullName,
    string Email,
    string UserName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    DateTime CreatedAt);
