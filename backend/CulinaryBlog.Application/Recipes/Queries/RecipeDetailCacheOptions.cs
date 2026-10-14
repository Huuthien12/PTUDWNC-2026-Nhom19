namespace CulinaryBlog.Application.Recipes.Queries;

public sealed class RecipeDetailCacheOptions
{
    public const string SectionName = "Cache:RecipeDetail";

    // Related mutation invalidation is complete; configuration can still disable the cache.
    public bool Enabled { get; set; } = true;
}
