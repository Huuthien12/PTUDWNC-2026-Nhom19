using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeImageOperationLockTests
{
    [PostgresFact]
    public async Task Advisory_lock_serializes_independent_database_connections_and_cancellation_releases_waiter()
    {
        await using var database = new RecipeDatabaseFixture();
        var recipeId = await database.InitializeAsync();
        await using var firstDb = database.NewContext();
        await using var secondDb = database.NewContext();
        var firstLock = new PostgresRecipeImageOperationLock(firstDb);
        var secondLock = new PostgresRecipeImageOperationLock(secondDb);

        await using var firstLease = await firstLock.AcquireAsync(recipeId);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => secondLock.AcquireAsync(recipeId, cancellation.Token));

        await firstLease.DisposeAsync();
        await using var secondLease = await secondLock.AcquireAsync(recipeId);
    }
}
