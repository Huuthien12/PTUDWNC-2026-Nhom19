using CulinaryBlog.Application.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
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

    private static Recipe Recipe(string slug, string title, RecipeStatus status, Recipe source, bool deleted = false) => new()
    {
        Title = title, Slug = slug, Description = title, Status = status, IsDeleted = deleted,
        CategoryId = source.CategoryId, AuthorId = source.AuthorId, Nutrition = new RecipeNutrition()
    };
}
