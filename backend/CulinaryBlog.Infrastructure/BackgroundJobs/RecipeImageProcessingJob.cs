using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Hangfire;
using CulinaryBlog.Application.Recipes.Images;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class RecipeImageProcessingJob(
    IFileStorageService storage,
    IUnitOfWork unitOfWork,
    IImageCleanupQueue cleanup,
    IRecipeCacheInvalidator? invalidator,
    ILogger<RecipeImageProcessingJob> logger,
    TimeProvider clock)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAsync(Guid recipeId, Guid imageId, string originalObjectKey)
    {
        var recipe = await unitOfWork.Recipes.GetForImagesAsync(recipeId);
        var image = recipe?.Images.FirstOrDefault(item => item.Id == imageId);
        if (recipe is null || image is null ||
            !RecipeImageStorageKey.TryFromUrl(image.OriginalUrl, recipeId, out var sourceKey) ||
            sourceKey != originalObjectKey)
            return;

        if (image.MediumUrl is not null && image.ThumbnailUrl is not null)
        {
            if (invalidator is not null)
                await invalidator.RecipeChangedAsync(recipe.Slug, CancellationToken.None);
            return;
        }

        var variants = await storage.CreateVariantsAsync(recipeId, originalObjectKey);
        image.MediumUrl = variants.Medium.Url;
        image.ThumbnailUrl = variants.Thumbnail.Url;
        recipe.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        unitOfWork.Recipes.UpdateForChildMutation(recipe, recipe.RowVersion);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            await DeleteVariantsAsync(variants);
            logger.LogError(exception, "Could not save processed image variants for recipe {RecipeId}, image {ImageId}.", recipeId, imageId);
            throw;
        }
        if (invalidator is not null)
            await invalidator.RecipeChangedAsync(recipe.Slug, CancellationToken.None);

        async Task DeleteVariantsAsync(StoredImageVariants stored)
        {
            foreach (var objectKey in new[] { stored.Medium.ObjectKey, stored.Thumbnail.ObjectKey })
            {
                try
                {
                    await storage.DeleteAsync(objectKey);
                }
                catch (Exception cleanupException)
                {
                    logger.LogError(cleanupException, "Could not clean up orphaned image variant {ObjectKey}.", objectKey);
                    try { cleanup.EnqueueDelete(objectKey); }
                    catch (Exception queueException)
                    {
                        logger.LogError(queueException, "Could not queue cleanup for image variant {ObjectKey}.", objectKey);
                    }
                }
            }
        }
    }
}
