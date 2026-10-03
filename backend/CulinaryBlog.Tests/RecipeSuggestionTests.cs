using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeSuggestionTests : IAsyncLifetime
{
    private readonly RecipeApiFactory _factory = new();

    public Task InitializeAsync()
        => _factory.InitializeAsync();

    public async Task DisposeAsync()
        => await _factory.DisposeAsync();

    [Fact]
    public async Task Suggestions_QueryShorterThanTwoCharacters_Returns400()
    {
        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=p");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Suggestions_ReturnsPublishedRecipeTitle()
    {
        await using var db = _factory.Database.NewContext();

        var recipe = new Recipe
        {
            Title = "Pho Ga",
            Slug = "pho-ga",
            Description = "Published recipe",
            Instructions = "Cook",
            PrepTime = 10,
            CookTime = 30,
            Servings = 2,
            Difficulty = RecipeDifficulty.Easy,
            Status = RecipeStatus.Published,
            CategoryId = _factory.CategoryId,
            AuthorId = _factory.AuthorId
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=pho");

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<RecipeSuggestionsResponse>();

        Assert.NotNull(result);

        Assert.Contains(
            result!.Items,
            item =>
                item.Text == "Pho Ga" &&
                item.Type == "recipe" &&
                item.Slug == "pho-ga");
    }

    [Fact]
    public async Task Suggestions_DoesNotReturnDraftRecipe()
    {
        await using var db = _factory.Database.NewContext();

        var recipe = new Recipe
        {
            Title = "Pho Draft",
            Slug = "pho-draft",
            Description = "Draft recipe",
            Instructions = "Draft",
            PrepTime = 10,
            CookTime = 20,
            Servings = 2,
            Difficulty = RecipeDifficulty.Easy,
            Status = RecipeStatus.Draft,
            CategoryId = _factory.CategoryId,
            AuthorId = _factory.AuthorId
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=pho");

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<RecipeSuggestionsResponse>();

        Assert.NotNull(result);

        Assert.DoesNotContain(
            result!.Items,
            item => item.Text == "Pho Draft");
    }

    [Fact]
    public async Task Suggestions_ReturnsCategorySuggestion()
    {
        await using var db = _factory.Database.NewContext();

        var category = new Category
        {
            Name = "Pho Vietnamese",
            Slug = "pho-vietnamese"
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync();

        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=pho");

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<RecipeSuggestionsResponse>();

        Assert.NotNull(result);

        Assert.Contains(
            result!.Items,
            item =>
                item.Text == "Pho Vietnamese" &&
                item.Type == "category" &&
                item.Slug == "pho-vietnamese");
    }

    [Fact]
    public async Task Suggestions_ReturnsAtMostTenItems()
    {
        await using var db = _factory.Database.NewContext();

        for (var i = 0; i < 15; i++)
        {
            db.Recipes.Add(new Recipe
            {
                Title = $"Pho Recipe {i:00}",
                Slug = $"pho-recipe-{i:00}",
                Description = "Published recipe",
                Instructions = "Cook",
                PrepTime = 10,
                CookTime = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Status = RecipeStatus.Published,
                CategoryId = _factory.CategoryId,
                AuthorId = _factory.AuthorId
            });
        }

        await db.SaveChangesAsync();

        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=pho");

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<RecipeSuggestionsResponse>();

        Assert.NotNull(result);

        Assert.True(result!.Items.Count <= 10);
    }

    [Fact]
    public async Task Suggestions_DeduplicatesSameTypeAndText()
    {
        await using var db = _factory.Database.NewContext();

        db.Recipes.Add(new Recipe
        {
            Title = "Pho",
            Slug = "pho",
            Description = "Published recipe",
            Instructions = "Cook",
            PrepTime = 10,
            CookTime = 20,
            Servings = 2,
            Difficulty = RecipeDifficulty.Easy,
            Status = RecipeStatus.Published,
            CategoryId = _factory.CategoryId,
            AuthorId = _factory.AuthorId
        });

        db.Categories.Add(new Category
        {
            Name = "Pho",
            Slug = "pho-category"
        });

        await db.SaveChangesAsync();

        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=pho");

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<RecipeSuggestionsResponse>();

        Assert.NotNull(result);

        var matchingItems = result!.Items
            .Where(item =>
                item.Text.Equals(
                    "Pho",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, matchingItems.Count);
    }

    [Fact]
    public async Task Suggestions_ExactMatchIsOrderedBeforePrefixMatch()
    {
        await using var db = _factory.Database.NewContext();

        db.Recipes.AddRange(
            new Recipe
            {
                Title = "Pho",
                Slug = "pho",
                Description = "Published recipe",
                Instructions = "Cook",
                PrepTime = 10,
                CookTime = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Status = RecipeStatus.Published,
                CategoryId = _factory.CategoryId,
                AuthorId = _factory.AuthorId
            },
            new Recipe
            {
                Title = "Pho Ga",
                Slug = "pho-ga-ordering",
                Description = "Published recipe",
                Instructions = "Cook",
                PrepTime = 10,
                CookTime = 20,
                Servings = 2,
                Difficulty = RecipeDifficulty.Easy,
                Status = RecipeStatus.Published,
                CategoryId = _factory.CategoryId,
                AuthorId = _factory.AuthorId
            });

        await db.SaveChangesAsync();

        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=pho");

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<RecipeSuggestionsResponse>();

        Assert.NotNull(result);

        var recipeItems = result!.Items
            .Where(item => item.Type == "recipe")
            .ToList();

        Assert.NotEmpty(recipeItems);

        Assert.Equal("Pho", recipeItems[0].Text);
    }

    [Fact]
    public async Task Suggestions_IsPublicWithoutAuthentication()
    {
        using var client = _factory.Client(role: null);

        var response = await client.GetAsync(
            "/api/v1/recipes/suggestions?q=ph");

        Assert.NotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.NotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private sealed record RecipeSuggestionsResponse(
        IReadOnlyList<RecipeSuggestionResponse> Items);

    private sealed record RecipeSuggestionResponse(
        string Text,
        string Type,
        string? Slug);
}
