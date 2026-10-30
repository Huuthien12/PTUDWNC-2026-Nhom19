using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RefreshTokenStoreTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private RefreshToken _original = null!;

    public async Task InitializeAsync()
    {
        await using var db = _factory.NewContext();
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner" });
        _original = NewToken("original");
        db.RefreshTokens.Add(_original);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private RefreshToken NewToken(string hash) => new()
    {
        UserId = "owner", Token = hash, ExpiresAt = _now.AddDays(7)
    };

    [Fact]
    public async Task Concurrent_rotation_has_exactly_one_winner_and_one_replacement()
    {
        // Independent DbContexts/connections, with both clients reading the same active token first.
        await using var firstDb = _factory.NewContext();
        await using var secondDb = _factory.NewContext();
        var first = new RefreshTokenStore(firstDb);
        var second = new RefreshTokenStore(secondDb);
        Assert.False((await first.FindAsync("original", default))!.IsRevoked);
        Assert.False((await second.FindAsync("original", default))!.IsRevoked);
        using var gate = new Barrier(2);
        var results = await Task.WhenAll(
            Task.Run(async () => { gate.SignalAndWait(); return await first.TryRotateAsync(_original.Id, NewToken("first"), _now, default); }),
            Task.Run(async () => { gate.SignalAndWait(); return await second.TryRotateAsync(_original.Id, NewToken("second"), _now, default); }));
        Assert.Single(results, won => won);
        await using var check = _factory.NewContext();
        Assert.Equal(2, await check.RefreshTokens.CountAsync());
        var original = await check.RefreshTokens.SingleAsync(t => t.Id == _original.Id);
        var replacement = await check.RefreshTokens.SingleAsync(t => !t.IsRevoked);
        Assert.Equal(replacement.Token, original.ReplacedByToken);
        Assert.Equal(_now, original.RevokedAt);
    }

    [Fact]
    public async Task Replacement_insert_failure_rolls_back_old_token_revocation()
    {
        await using (var db = _factory.NewContext())
        {
            var invalidReplacement = NewToken("replacement");
            invalidReplacement.UserId = "nonexistent-user"; // Force a real relational FK failure after the update.
            await Assert.ThrowsAsync<DbUpdateException>(() => new RefreshTokenStore(db)
                .TryRotateAsync(_original.Id, invalidReplacement, _now, default));
        }
        await using var check = _factory.NewContext();
        var original = await check.RefreshTokens.SingleAsync();
        Assert.False(original.IsRevoked);
        Assert.Null(original.RevokedAt);
        Assert.Null(original.ReplacedByToken);
    }

    [Fact]
    public async Task Logout_winning_after_refresh_read_prevents_rotation()
    {
        await using var readerDb = _factory.NewContext();
        var reader = new RefreshTokenStore(readerDb);
        Assert.False((await reader.FindAsync("original", default))!.IsRevoked);
        await using var logoutDb = _factory.NewContext();
        Assert.True(await new RefreshTokenStore(logoutDb).RevokeAsync("original", "owner", _now, false, default));
        Assert.False(await reader.TryRotateAsync(_original.Id, NewToken("replacement"), _now, default));
        Assert.Equal(1, await readerDb.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Store_rechecks_expiry_and_replacement_even_if_caller_has_stale_state()
    {
        await using var db = _factory.NewContext();
        var store = new RefreshTokenStore(db);
        Assert.False(await store.TryRotateAsync(_original.Id, NewToken("expired"), _original.ExpiresAt, default));
        await db.RefreshTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedByToken, "already-replaced"));
        Assert.False(await store.TryRotateAsync(_original.Id, NewToken("replaced"), _now, default));
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
    }
}
