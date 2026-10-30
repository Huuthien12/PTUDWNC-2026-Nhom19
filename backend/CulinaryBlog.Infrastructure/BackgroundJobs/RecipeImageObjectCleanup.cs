using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Images;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class RecipeImageObjectCleanup(
    AppDbContext db,
    IFileStorageService storage,
    IRecipeImageOperationLock operationLock) : IRecipeImageObjectCleanup
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);

    public async Task<bool> DeleteIfUnreferencedAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (!RecipeImageStorageKey.TryGetRecipeIdFromObjectKey(objectKey, out var recipeId))
            throw new ArgumentException("Object key is not a recipe image object.", nameof(objectKey));

        using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationTimeout.CancelAfter(OperationTimeout);
        var operationToken = operationTimeout.Token;

        await using var lease = await operationLock.AcquireAsync(recipeId, operationToken);
        var images = await db.RecipeImages.IgnoreQueryFilters().AsNoTracking()
            .Where(image => image.RecipeId == recipeId)
            .Select(image => new { image.Id, image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl })
            .ToListAsync(operationToken);

        foreach (var image in images)
        foreach (var url in new[] { image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl })
            if (RecipeImageStorageKey.TryFromUrl(url, recipeId, out var referencedKey) &&
                string.Equals(referencedKey, objectKey, StringComparison.Ordinal))
                return false;

        await storage.DeleteAsync(objectKey, operationToken);
        return true;
    }
}
