using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class RecipeImageProcessingJob(
    IFileStorageService storage,
    IUnitOfWork unitOfWork,
    IImageCleanupQueue cleanup,
    IRecipeCacheInvalidator? invalidator,
    ILogger<RecipeImageProcessingJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAsync(Guid recipeId, Guid imageId, string originalObjectKey)
    {
        var variants = await storage.CreateVariantsAsync(recipeId, originalObjectKey);
        var recipe = await unitOfWork.Recipes.GetForImagesAsync(recipeId);
        var image = recipe?.Images.FirstOrDefault(item => item.Id == imageId);
        if (recipe is null || image is null)
        {
            await DeleteVariantsAsync(variants);
            return;
        }

        image.MediumUrl = variants.Medium.Url;
        image.ThumbnailUrl = variants.Thumbnail.Url;
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
                    cleanup.EnqueueDelete(objectKey);
                }
            }
        }
    }
}
