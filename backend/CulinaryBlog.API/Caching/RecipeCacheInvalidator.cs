using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.API.Caching;

public sealed class RecipeCacheInvalidator(AppDbContext db, ICacheService cache,
    IOutputCacheStore output, ILogger<RecipeCacheInvalidator> logger) : IRecipeCacheInvalidator
{
    private const string SuggestionVersionKey = "search:suggest:version";

    public async Task RecipeChangedAsync(string slug, CancellationToken cancellationToken)
    {
        await cache.RemoveSafelyAsync($"recipe:{slug}", cancellationToken, logger);
        await ListChangedAsync(cancellationToken);
    }

    public async Task CategoryChangedAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        // Relational projection, never enumerate Redis keys. DB errors are not cache errors.
        var slugs = await db.Recipes.AsNoTracking().Where(r => r.CategoryId == categoryId)
            .Select(r => r.Slug).ToListAsync(cancellationToken);
        await EvictAsync(slugs, cancellationToken);
    }

    public async Task AuthorChangedAsync(string authorId, CancellationToken cancellationToken)
    {
        var slugs = await db.Recipes.AsNoTracking().Where(r => r.AuthorId == authorId)
            .Select(r => r.Slug).ToListAsync(cancellationToken);
        await EvictAsync(slugs, cancellationToken);
    }

    private async Task EvictAsync(IEnumerable<string> slugs, CancellationToken cancellationToken)
    {
        foreach (var slug in slugs.Distinct())
            await cache.RemoveSafelyAsync($"recipe:{slug}", cancellationToken, logger);
        await ListChangedAsync(cancellationToken);
    }

    public async Task ListChangedAsync(CancellationToken cancellationToken)
    {
        try { await output.EvictByTagAsync("recipes", cancellationToken); }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "recipe-list tag eviction", error); }
    }

    public async Task<string?> GetSuggestionVersionAsync(CancellationToken cancellationToken)
    {
        try { return await cache.GetAsync<string>(SuggestionVersionKey, cancellationToken) ?? "0"; }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "recipe-suggestion version read", error); return null; }
    }

    public async Task InvalidateSuggestionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetAsync(SuggestionVersionKey, Guid.NewGuid().ToString("N"),
                TimeSpan.FromDays(1), cancellationToken, useAbsoluteExpiration: true);
        }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "recipe-suggestion invalidation", error); }
    }
}
