using CulinaryBlog.Infrastructure.Persistence.Seed;

namespace CulinaryBlog.SeedUpgradeCli;

public sealed record TargetCatalogSnapshot(
    int RecipeNumber,
    string Title,
    string Description,
    string Instructions,
    string CategoryName,
    int PrepTime,
    int CookTime,
    int Servings,
    object Difficulty,
    IReadOnlyList<TargetIngredientSnapshot> Ingredients,
    IReadOnlyList<TargetStepSnapshot> Steps);

public sealed record TargetIngredientSnapshot(int OrderIndex, string Name, decimal Quantity, string Unit, string? Notes);
public sealed record TargetStepSnapshot(int StepNumber, string Title, string Description, int? TimerMinutes);

public static class SeedUpgradeCatalog
{
    public static IReadOnlyList<RealRecipeSeed> Recipes { get; } = RealRecipeSeedBatch01.Items
        .Concat(RealRecipeSeedBatch02.Items).Concat(RealRecipeSeedBatch03.Items)
        .Concat(RealRecipeSeedBatch04.Items).Concat(RealRecipeSeedBatch05.Items).ToList();

    public static TargetCatalogSnapshot Snapshot(int recipeNumber)
    {
        if (recipeNumber is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(recipeNumber));
        var recipe = Recipes[recipeNumber - 1];
        return new(recipeNumber, recipe.Title, recipe.Description,
            string.Join("\n\n", recipe.Steps.Select(step => $"{step.Title}: {step.Description}")),
            recipe.CategoryName, recipe.PrepTime, recipe.CookTime, recipe.Servings, recipe.Difficulty,
            recipe.Ingredients.Select((ingredient, index) => new TargetIngredientSnapshot(index + 1, ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.Notes)).ToList(),
            recipe.Steps.Select((step, index) => new TargetStepSnapshot(index + 1, step.Title, step.Description, step.TimerMinutes)).ToList());
    }

    public static string Hash(int recipeNumber) => SnapshotHashing.Hash(Snapshot(recipeNumber));

    public static string NormalizeCategory(string value) => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
        .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark))
        .Replace(" ", string.Empty).ToLowerInvariant();
}
