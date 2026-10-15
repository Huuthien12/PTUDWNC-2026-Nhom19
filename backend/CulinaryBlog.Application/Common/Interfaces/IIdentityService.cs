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

    Task<IdentityPasswordChangeResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<PasswordResetTokenResult?> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<IdentityPasswordResetResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
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
    string? AvatarUrl = null,
    DateTimeOffset? LockoutEnd = null);

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

public sealed record IdentityPasswordChangeResult(
    bool Succeeded,
    string? ErrorCode,
    DateTimeOffset? LockoutEnd = null);

public sealed record PasswordResetTokenResult(
    string Email,
    string FullName,
    string Token);

public sealed record IdentityPasswordResetResult(
    bool Succeeded,
    string? UserId,
    string? ErrorCode);
