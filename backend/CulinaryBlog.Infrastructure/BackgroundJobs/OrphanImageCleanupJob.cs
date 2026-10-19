using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Images;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class OrphanImageCleanupJob(AppDbContext db, IFileStorageService storage,
    IImageProcessingQueue processing, TimeProvider clock, ILogger<OrphanImageCleanupJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        // Include soft-deleted rows: a file with a DB reference is never an orphan.
        // Load DB references before any delete; a database failure must leave storage untouched.
        var images = await db.RecipeImages.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        var referenced = images.SelectMany(image => RecipeImageStorageKey.ForImage(image.RecipeId, image))
            .ToHashSet(StringComparer.Ordinal);
        var scanned = 0;
        var deleted = 0;
        await foreach (var item in storage.ListRecipeObjectsAsync())
        {
            scanned++;
            if (!item.ObjectKey.StartsWith("recipes/", StringComparison.Ordinal) ||
                item.LastModifiedUtc >= cutoff || referenced.Contains(item.ObjectKey)) continue;
            try
            {
                await storage.DeleteAsync(item.ObjectKey);
                deleted++;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not delete orphan image object {ObjectKey}.", item.ObjectKey);
            }
        }
        logger.LogInformation("Orphan image cleanup scanned {Scanned}, deleted {Deleted}, skipped {Skipped} objects.",
            scanned, deleted, scanned - deleted);

        // Recover uploads whose post-commit enqueue failed or whose worker stopped before saving URLs.
        foreach (var image in await db.RecipeImages.AsNoTracking()
            .Where(image => image.CreatedAt < cutoff && (image.MediumUrl == null || image.ThumbnailUrl == null))
            .ToListAsync())
        {
            if (RecipeImageStorageKey.TryFromUrl(image.OriginalUrl, image.RecipeId, out var key))
                processing.EnqueueProcess(image.RecipeId, image.Id, key);
        }
    }
}
