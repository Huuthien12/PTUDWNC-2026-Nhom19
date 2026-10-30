using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Application.Recipes.Images;

internal static class RecipeImageAccess
{
    // 404 when the recipe does not exist (or is soft-deleted), 403 when the caller is neither owner nor Admin.
    public static async Task<Recipe> LoadForMutationAsync(IRecipeRepository recipes, Guid recipeId,
        string userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var recipe = await recipes.GetForImagesAsync(recipeId, cancellationToken)
            ?? throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe not found.");
        if (!isAdmin && recipe.AuthorId != userId)
            throw new ForbiddenException("RECIPE_FORBIDDEN", "Only the author or an Admin may manage this recipe's images.");
        return recipe;
    }

    // Best effort: if the queue itself is down, the orphan sweep (FR-JOB-003) collects the file later.
    public static void TryEnqueueDelete(IImageCleanupQueue queue, string objectKey)
    {
        try { queue.EnqueueDelete(objectKey); }
        catch (Exception) { /* swallowed on purpose: DB state is already final */ }
    }
}
