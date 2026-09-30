using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeLifecycleTests
{
    [Fact]
    public void Publish_draft_with_an_active_ingredient_and_step_sets_published()
    {
        var recipe = CreateRecipe();
        recipe.Ingredients.Add(new RecipeIngredient());
        recipe.Steps.Add(new RecipeStep());

        recipe.Publish();

        Assert.Equal(RecipeStatus.Published, recipe.Status);
    }

    [Fact]
    public void Publish_requires_an_active_ingredient()
    {
        var recipe = CreateRecipe();
        recipe.Ingredients.Add(new RecipeIngredient { IsDeleted = true });
        recipe.Steps.Add(new RecipeStep());

        var exception = Assert.Throws<BusinessRuleException>(recipe.Publish);

        Assert.Equal("RECIPE_PUBLISH_INCOMPLETE", exception.ErrorCode);
    }

    [Fact]
    public void Publish_requires_an_active_step()
    {
        var recipe = CreateRecipe();
        recipe.Ingredients.Add(new RecipeIngredient());
        recipe.Steps.Add(new RecipeStep { IsDeleted = true });

        var exception = Assert.Throws<BusinessRuleException>(recipe.Publish);

        Assert.Equal("RECIPE_PUBLISH_INCOMPLETE", exception.ErrorCode);
    }

    [Fact]
    public void Publish_is_idempotent_for_a_published_recipe()
    {
        var recipe = CreateRecipe(RecipeStatus.Published);

        recipe.Publish();

        Assert.Equal(RecipeStatus.Published, recipe.Status);
    }

    [Fact]
    public void Unpublish_sets_a_published_recipe_to_draft()
    {
        var recipe = CreateRecipe(RecipeStatus.Published);

        recipe.Unpublish();

        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Fact]
    public void Unpublish_is_idempotent_for_a_draft_recipe()
    {
        var recipe = CreateRecipe();

        recipe.Unpublish();

        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    private static Recipe CreateRecipe(RecipeStatus status = RecipeStatus.Draft) => new()
    {
        Status = status
    };
}
