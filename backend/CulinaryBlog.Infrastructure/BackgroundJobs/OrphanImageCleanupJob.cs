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
    private const int BatchSize = 200;

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RunAsync()
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        // Include soft-deleted rows: a file with a DB reference is never an orphan.
        // Load DB references before any delete; a database failure must leave storage untouched.
        var referenced = await LoadReferencedKeysAsync();
        var scanned = 0;
        var deleted = 0;
        var failures = new List<Exception>();
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
                failures.Add(exception);
            }
        }
        logger.LogInformation("Orphan image cleanup scanned {Scanned}, deleted {Deleted}, skipped {Skipped} objects.",
            scanned, deleted, scanned - deleted);

        if (failures.Count > 0)
            throw new AggregateException("One or more orphan image objects could not be deleted.", failures);
    }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RecoverPendingAsync()
    {
        var failures = new List<Exception>();
        Guid? lastId = null;
        while (true)
        {
            var query = db.RecipeImages.AsNoTracking()
                .Where(image => image.MediumUrl == null || image.ThumbnailUrl == null);
            if (lastId.HasValue) query = query.Where(image => image.Id.CompareTo(lastId.Value) > 0);
            var images = await query
                .OrderBy(image => image.Id)
                .Select(image => new { image.RecipeId, image.Id, image.OriginalUrl })
                .Take(BatchSize).ToListAsync();
            foreach (var image in images)
            {
                if (!RecipeImageStorageKey.TryFromUrl(image.OriginalUrl, image.RecipeId, out var key)) continue;
                try { processing.EnqueueProcess(image.RecipeId, image.Id, key); }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not recover image processing for recipe {RecipeId}, image {ImageId}.",
                        image.RecipeId, image.Id);
                    failures.Add(exception);
                }
            }
            if (images.Count < BatchSize) break;
            lastId = images[^1].Id;
        }
        if (failures.Count > 0)
            throw new AggregateException("One or more pending images could not be queued for processing.", failures);
    }

    private async Task<HashSet<string>> LoadReferencedKeysAsync()
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        Guid? lastId = null;
        while (true)
        {
            var query = db.RecipeImages.IgnoreQueryFilters().AsNoTracking();
            if (lastId.HasValue) query = query.Where(image => image.Id.CompareTo(lastId.Value) > 0);
            var images = await query
                .OrderBy(image => image.Id)
                .Select(image => new
                {
                    image.Id,
                    image.RecipeId,
                    image.OriginalUrl,
                    image.MediumUrl,
                    image.ThumbnailUrl
                })
                .Take(BatchSize).ToListAsync();
            foreach (var image in images)
            foreach (var url in new[] { image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl })
                if (RecipeImageStorageKey.TryFromUrl(url, image.RecipeId, out var key)) referenced.Add(key);
            if (images.Count < BatchSize) return referenced;
            lastId = images[^1].Id;
        }
    }
}
