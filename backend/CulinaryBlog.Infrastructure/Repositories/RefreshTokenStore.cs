using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class RefreshTokenStore(AppDbContext context) : IRefreshTokenStore
{
    public async Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // The legacy schema has no unique constraint. Ambiguous hashes must not
        // select an arbitrary session or turn a refresh request into a 500.
        var matches = await context.RefreshTokens.AsNoTracking()
            .Where(t => t.Token == tokenHash).Take(2).ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryRotateAsync(Guid id, RefreshToken replacement, DateTime now, CancellationToken cancellationToken)
    {
        // Npgsql's default is ReadCommitted: a competing UPDATE waits, then
        // rechecks this predicate against the committed row. Zero rows is a
        // rejected rotation, not an EF tracked-entity concurrency exception.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var changed = await context.RefreshTokens
            .Where(t => t.Id == id && !t.IsDeleted && !t.IsRevoked && t.ReplacedByToken == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.UpdatedAt, now)
                .SetProperty(t => t.ReplacedByToken, replacement.Token), cancellationToken);
        if (changed == 0)
            return false;

        context.RefreshTokens.Add(replacement);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeAsync(string tokenHash, string userId, DateTime now, bool requireActive, CancellationToken cancellationToken)
    {
        var query = context.RefreshTokens.Where(t => t.Token == tokenHash && t.UserId == userId && !t.IsRevoked && !t.IsDeleted);
        if (requireActive)
            query = query.Where(t => t.ExpiresAt > now && t.ReplacedByToken == null);

        return await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(t => t.IsRevoked, true)
            .SetProperty(t => t.RevokedAt, now)
            .SetProperty(t => t.UpdatedAt, now), cancellationToken) > 0;
    }
}
