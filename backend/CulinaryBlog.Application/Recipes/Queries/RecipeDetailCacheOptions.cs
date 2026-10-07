namespace CulinaryBlog.Application.Recipes.Queries;

public sealed class RecipeDetailCacheOptions
{
    public const string SectionName = "Cache:RecipeDetail";

    // Keep disabled until all mutations invalidate Recipe Detail entries.
    public bool Enabled { get; set; } = false;
}
