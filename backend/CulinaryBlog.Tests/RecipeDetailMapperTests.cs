using CulinaryBlog.Application.Recipes.Mappers;
using CulinaryBlog.Domain.Entities;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeDetailMapperTests
{
    [Fact]
    public void Maps_the_persisted_row_version_as_canonical_base64_without_mutating_it()
    {
        var rowVersion = new byte[] { 1, 2, 3, 4 };
        var recipe = new Recipe
        {
            RowVersion = rowVersion,
            Category = new Category { Name = "Category", Slug = "category" },
            Author = new ApplicationUser { FullName = "Author" }
        };

        var result = RecipeDetailMapper.ToDto(recipe);

        Assert.Equal(Convert.ToBase64String(rowVersion), result.RowVersion);
        Assert.Equal(rowVersion, Convert.FromBase64String(result.RowVersion));
        Assert.Equal(rowVersion, recipe.RowVersion);
    }
}
