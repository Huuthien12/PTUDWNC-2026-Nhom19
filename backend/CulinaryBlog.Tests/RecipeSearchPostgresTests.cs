using CulinaryBlog.Application.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeSearchPostgresTests
{
    [PostgresFact]
    public async Task Fts_matches_unaccented_prefixes_and_excludes_non_public_recipes()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        await using var db = database.NewContext();
        var source = await db.Recipes.SingleAsync(recipe => recipe.Id == id);
        db.Recipes.AddRange(
            Recipe("bánh-mì", "Bánh mì", RecipeStatus.Published, source),
            Recipe("bánh-bao", "Bánh bao", RecipeStatus.Published, source),
            Recipe("bánh-draft", "Bánh draft", RecipeStatus.Draft, source),
            Recipe("bánh-archived", "Bánh archived", RecipeStatus.Archived, source),
            Recipe("bánh-deleted", "Bánh deleted", RecipeStatus.Published, source, true));
        await db.SaveChangesAsync();
        var handler = new SearchRecipesQueryHandler(new UnitOfWork(db, new CategoryRepository(db), new RecipeRepository(db)));

        var page = await handler.Handle(new SearchRecipesQuery("banh", 1, 1), default);
        var second = await handler.Handle(new SearchRecipesQuery("banh", 2, 1), default);
        var noMatch = await handler.Handle(new SearchRecipesQuery("khongco", 1, 12), default);

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Single(second.Items);
        Assert.DoesNotContain(page.Items.Concat(second.Items), item => item.Slug.Contains("draft") || item.Slug.Contains("archived") || item.Slug.Contains("deleted"));
        Assert.Empty(noMatch.Items);
    }

    [PostgresFact]
    public async Task Suggestions_match_unaccented_vietnamese_prefixes()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        await using var db = database.NewContext();
        var source = await db.Recipes.SingleAsync(recipe => recipe.Id == id);
        db.Recipes.AddRange(
            Recipe("tom-nuong", "Tôm nướng muối ớt", RecipeStatus.Published, source),
            Recipe("pho-bo", "Phở bò", RecipeStatus.Published, source),
            Recipe("banh-xeo", "Bánh xèo", RecipeStatus.Published, source));
        await db.SaveChangesAsync();

        var repository = new RecipeRepository(db);

        var tom = await repository.GetSuggestionsAsync("tom");
        var pho = await repository.GetSuggestionsAsync("pho");
        var banh = await repository.GetSuggestionsAsync("banh");

        Assert.Contains(tom, item => item.Text == "Tôm nướng muối ớt");
        Assert.Contains(pho, item => item.Text == "Phở bò");
        Assert.Contains(banh, item => item.Text == "Bánh xèo");
    }

    [PostgresFact]
    public async Task Search_cache_uses_public_page_key_ttl_and_falls_back_when_cache_is_unavailable()
    {
        await using var database = new RecipeDatabaseFixture();
        await database.InitializeAsync();
        await using var db = database.NewContext();
        var unitOfWork = new UnitOfWork(db, new CategoryRepository(db), new RecipeRepository(db));
        var invalidator = new RecordingRecipeInvalidator();
        var cache = new RecordingRecipeCache();
        var handler = new SearchRecipesQueryHandler(unitOfWork, cache, invalidator,
            NullLogger<SearchRecipesQueryHandler>.Instance);

        await handler.Handle(new SearchRecipesQuery("original", 2, 7), default);

        Assert.Contains(cache.SetOperations, operation =>
            operation.Key == "search:recipes:v0:original:2:7" &&
            operation.Expiration == TimeSpan.FromSeconds(60) && operation.UseAbsoluteExpiration);

        var unavailable = new SearchRecipesQueryHandler(unitOfWork, new UnavailableCache(), invalidator,
            NullLogger<SearchRecipesQueryHandler>.Instance);
        var result = await unavailable.Handle(new SearchRecipesQuery("original"), default);
        Assert.NotNull(result);
    }

    private sealed class UnavailableCache : CulinaryBlog.Application.Common.Interfaces.ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            throw new IOException("cache unavailable");
        public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default,
            bool useAbsoluteExpiration = false) => throw new IOException("cache unavailable");
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static Recipe Recipe(string slug, string title, RecipeStatus status, Recipe source, bool deleted = false) => new()
    {
        Title = title, Slug = slug, Description = title, Status = status, IsDeleted = deleted,
        CategoryId = source.CategoryId, AuthorId = source.AuthorId, Nutrition = new RecipeNutrition()
    };
}
