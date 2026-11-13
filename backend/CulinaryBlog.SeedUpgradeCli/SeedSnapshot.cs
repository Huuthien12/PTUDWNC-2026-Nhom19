using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.SeedUpgradeCli;

public sealed record RecipeNutritionSnapshot(decimal? Calories, decimal? Protein, decimal? Carbs, decimal? Fat);
public sealed record RecipeIngredientSnapshot(Guid Id, string Name, decimal? Quantity, string? Unit, string? Notes, int OrderIndex, DateTime CreatedAt, DateTime UpdatedAt, bool IsDeleted);
public sealed record RecipeStepSnapshot(Guid Id, int StepNumber, string? Title, string Description, int? TimerMinutes, string? ImageUrl, DateTime CreatedAt, DateTime UpdatedAt, bool IsDeleted);
public sealed record RecipeImageSnapshot(Guid Id, string OriginalUrl, string? MediumUrl, string? ThumbnailUrl, string? AltText, bool IsPrimary, int OrderIndex, DateTime CreatedAt, DateTime UpdatedAt, bool IsDeleted);
public sealed record RecipeAggregateSnapshot(Guid Id, string AuthorId, string Slug, string Title, string Description, string Instructions, Guid CategoryId, int PrepTime, int CookTime, int Servings, object Difficulty, object Status, DateTime? PublishedAt, DateTime CreatedAt, DateTime UpdatedAt, string RowVersion, RecipeNutritionSnapshot? Nutrition, IReadOnlyList<RecipeIngredientSnapshot> Ingredients, IReadOnlyList<RecipeStepSnapshot> Steps, IReadOnlyList<RecipeImageSnapshot> Images);
public sealed record RecipeSnapshotRow(string SnapshotHash, RecipeAggregateSnapshot Aggregate);
public sealed record FullSeedSnapshot(string SnapshotHash, IReadOnlyList<RecipeSnapshotRow> Recipes);

public static class SeedSnapshotFactory
{
    public static RecipeAggregateSnapshot Create(Recipe recipe) => new(
        recipe.Id, recipe.AuthorId, recipe.Slug, recipe.Title, recipe.Description, recipe.Instructions,
        recipe.CategoryId, recipe.PrepTime, recipe.CookTime, recipe.Servings, recipe.Difficulty, recipe.Status,
        recipe.PublishedAt, recipe.CreatedAt, recipe.UpdatedAt, Convert.ToBase64String(recipe.RowVersion),
        recipe.Nutrition is null ? null : new(recipe.Nutrition.Calories, recipe.Nutrition.Protein, recipe.Nutrition.Carbs, recipe.Nutrition.Fat),
        recipe.Ingredients.OrderBy(ingredient => ingredient.OrderIndex).ThenBy(ingredient => ingredient.Id)
            .Select(ingredient => new RecipeIngredientSnapshot(ingredient.Id, ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.Notes, ingredient.OrderIndex, ingredient.CreatedAt, ingredient.UpdatedAt, ingredient.IsDeleted)).ToList(),
        recipe.Steps.OrderBy(step => step.StepNumber).ThenBy(step => step.Id)
            .Select(step => new RecipeStepSnapshot(step.Id, step.StepNumber, step.Title, step.Description, step.TimerMinutes, step.ImageUrl, step.CreatedAt, step.UpdatedAt, step.IsDeleted)).ToList(),
        recipe.Images.OrderBy(image => image.OrderIndex).ThenBy(image => image.Id)
            .Select(image => new RecipeImageSnapshot(image.Id, image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl, image.AltText, image.IsPrimary, image.OrderIndex, image.CreatedAt, image.UpdatedAt, image.IsDeleted)).ToList());

    public static RecipeSnapshotRow CreateRow(Recipe recipe)
    {
        var aggregate = Create(recipe);
        return new RecipeSnapshotRow(SnapshotHashing.Hash(aggregate), aggregate);
    }
}
