using CulinaryBlog.Infrastructure.Persistence.Seed;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RealRecipeSeedCatalogTests
{
    [Fact]
    public void All_batches_define_one_hundred_distinct_complete_recipes()
    {
        var recipes = RealRecipeSeedBatch01.Items.Concat(RealRecipeSeedBatch02.Items)
            .Concat(RealRecipeSeedBatch03.Items).Concat(RealRecipeSeedBatch04.Items)
            .Concat(RealRecipeSeedBatch05.Items).ToList();
        Assert.Equal(100, recipes.Count);
        Assert.Equal(100, recipes.Select(x => x.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(20, recipes.Select(x => x.CategoryName).Distinct().Count());
        Assert.DoesNotContain(recipes, x => x.Title.StartsWith("Công thức #"));
        Assert.All(recipes, recipe =>
        {
            Assert.InRange(recipe.PrepTime, 1, 300); Assert.InRange(recipe.CookTime, 1, 360); Assert.InRange(recipe.Servings, 1, 12);
            Assert.InRange(recipe.Ingredients.Count, 5, 12); Assert.InRange(recipe.Steps.Count, 4, 8);
            Assert.All(recipe.Ingredients, x => { Assert.True(x.Quantity > 0); Assert.False(string.IsNullOrWhiteSpace(x.Unit)); });
            Assert.All(recipe.Steps, x => { Assert.False(string.IsNullOrWhiteSpace(x.Description)); Assert.DoesNotContain("phù hợp", x.Description, StringComparison.OrdinalIgnoreCase); });
        });
    }
}
