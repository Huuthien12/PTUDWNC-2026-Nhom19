namespace CulinaryBlog.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<IdentityLoginResult> FindActiveUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<IdentityLoginResult> LoginAsync(
        string email,
        string password,
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
