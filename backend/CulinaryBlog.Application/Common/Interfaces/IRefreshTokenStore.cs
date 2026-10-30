using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
    // Atomically consume the old token and persist its replacement, or change nothing.
    Task<bool> TryRotateAsync(Guid id, RefreshToken replacement, DateTime now, CancellationToken cancellationToken);
    Task RevokeAllAsync(string userId, DateTime now, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(string tokenHash, string userId, DateTime now, bool requireActive, CancellationToken cancellationToken);
}

public interface IRefreshTokenGenerator
{
    string Generate();
    string Hash(string token);
}
