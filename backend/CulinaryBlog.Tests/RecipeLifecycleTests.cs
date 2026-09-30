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
        var updatedAt = recipe.UpdatedAt;
        recipe.PublishedAt = DateTime.UtcNow.AddDays(-1);

        recipe.Publish();

        Assert.Equal(RecipeStatus.Published, recipe.Status);
        Assert.Equal(updatedAt, recipe.UpdatedAt);
    }

    [Fact]
    public void Unpublish_sets_a_published_recipe_to_draft()
    {
        var recipe = CreateRecipe(RecipeStatus.Published);
        var publishedAt = DateTime.UtcNow.AddDays(-1);
        recipe.PublishedAt = publishedAt;
        recipe.UpdatedAt = DateTime.UtcNow.AddDays(-2);

        recipe.Unpublish();

        Assert.Equal(RecipeStatus.Draft, recipe.Status);
        Assert.True(recipe.UpdatedAt > publishedAt);
        Assert.Equal(publishedAt, recipe.PublishedAt);
    }

    [Fact]
    public void Unpublish_is_idempotent_for_a_draft_recipe()
    {
        var recipe = CreateRecipe();

        recipe.Unpublish();

        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Theory]
    [InlineData(RecipeStatus.Archived, "RECIPE_PUBLISH_INVALID_STATE")]
    [InlineData(RecipeStatus.Archived, "RECIPE_UNPUBLISH_INVALID_STATE")]
    public void Invalid_lifecycle_transition_is_rejected(RecipeStatus status, string errorCode)
    {
        var recipe = CreateRecipe(status);
        Action action = errorCode == "RECIPE_PUBLISH_INVALID_STATE" ? recipe.Publish : recipe.Unpublish;

        var exception = Assert.Throws<BusinessRuleException>(action);

        Assert.Equal(errorCode, exception.ErrorCode);
    }

    [Theory]
    [InlineData(RecipeStatus.Draft)]
    [InlineData(RecipeStatus.Published)]
    public void Archive_moves_active_recipe_to_archived_and_preserves_published_at(RecipeStatus status)
    {
        var publishedAt = DateTime.UtcNow.AddDays(-2);
        var recipe = CreateRecipe(status);
        recipe.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        recipe.PublishedAt = publishedAt;

        recipe.Archive();

        Assert.Equal(RecipeStatus.Archived, recipe.Status);
        Assert.True(recipe.UpdatedAt > publishedAt);
        Assert.Equal(publishedAt, recipe.PublishedAt);
    }

    [Fact]
    public void Archive_is_idempotent_for_an_archived_recipe()
    {
        var recipe = CreateRecipe(RecipeStatus.Archived);
        var updatedAt = recipe.UpdatedAt;
        var publishedAt = DateTime.UtcNow.AddDays(-2);
        recipe.PublishedAt = publishedAt;

        recipe.Archive();

        Assert.Equal(RecipeStatus.Archived, recipe.Status);
        Assert.Equal(updatedAt, recipe.UpdatedAt);
        Assert.Equal(publishedAt, recipe.PublishedAt);
    }

    private static Recipe CreateRecipe(RecipeStatus status = RecipeStatus.Draft) => new()
    {
        Status = status
    };
}
