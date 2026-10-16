using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeDashboardApiTests
{
    private static Recipe Recipe(
        Guid categoryId,
        string authorId,
        string slug,
        RecipeStatus status,
        DateTime createdAt,
        bool deleted = false) => new()
    {
        Title = slug,
        Slug = slug,
        CategoryId = categoryId,
        AuthorId = authorId,
        Status = status,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        IsDeleted = deleted,
        Nutrition = new RecipeNutrition()
    };

    [Fact]
    public async Task Anonymous_is_unauthorized_and_non_author_role_is_forbidden()
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.Client(role: null).GetAsync("/api/v1/recipes/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await factory.Client(role: "Reader").GetAsync("/api/v1/recipes/mine")).StatusCode);
    }

    [Fact]
    public async Task Author_gets_only_own_non_deleted_recipes_in_every_status()
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        await using (var db = factory.Database.NewContext())
        {
            (await db.Recipes.SingleAsync()).CreatedAt = new DateTime(2026, 1, 1);
            db.Users.Add(new ApplicationUser { Id = "other", UserName = "other" });
            db.Recipes.AddRange(
                Recipe(factory.CategoryId, factory.AuthorId, "own-published", RecipeStatus.Published, new(2026, 1, 2)),
                Recipe(factory.CategoryId, factory.AuthorId, "own-archived", RecipeStatus.Archived, new(2026, 1, 3)),
                Recipe(factory.CategoryId, factory.AuthorId, "own-deleted", RecipeStatus.Draft, new(2026, 1, 4), true),
                Recipe(factory.CategoryId, "other", "other-published", RecipeStatus.Published, new(2026, 1, 5)),
                Recipe(factory.CategoryId, "other", "other-draft", RecipeStatus.Draft, new(2026, 1, 6)));
            await db.SaveChangesAsync();
        }

        var result = await factory.Client().GetFromJsonAsync<PagedResult<RecipeDashboardItemDto>>(
            "/api/v1/recipes/mine");

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["own-archived", "own-published", "original-recipe"],
            result.Items.Select(item => item.Slug));
        Assert.All(result.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.RowVersion)));
    }

    [Fact]
    public async Task Pagination_filters_owner_before_count_sort_and_page()
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        await using (var db = factory.Database.NewContext())
        {
            var original = await db.Recipes.SingleAsync();
            original.CreatedAt = new DateTime(2026, 1, 1);
            db.Users.Add(new ApplicationUser { Id = "other", UserName = "other" });
            for (var day = 2; day <= 5; day++)
                db.Recipes.Add(Recipe(factory.CategoryId, factory.AuthorId, $"mine-{day}", RecipeStatus.Draft, new(2026, 1, day)));
            for (var day = 10; day <= 15; day++)
                db.Recipes.Add(Recipe(factory.CategoryId, "other", $"other-{day}", RecipeStatus.Published, new(2026, 1, day)));
            await db.SaveChangesAsync();
        }

        var client = factory.Client();
        var first = await client.GetFromJsonAsync<PagedResult<RecipeDashboardItemDto>>(
            "/api/v1/recipes/mine?page=1&pageSize=2");
        var second = await client.GetFromJsonAsync<PagedResult<RecipeDashboardItemDto>>(
            "/api/v1/recipes/mine?page=2&pageSize=2");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal((5, 3), (first.TotalCount, first.TotalPages));
        Assert.Equal(["mine-5", "mine-4"], first.Items.Select(item => item.Slug));
        Assert.Equal(["mine-3", "mine-2"], second.Items.Select(item => item.Slug));
        Assert.DoesNotContain(first.Items.Concat(second.Items), item => item.Slug.StartsWith("other-"));
    }

    [Fact]
    public async Task Admin_mine_returns_only_admin_owned_recipes()
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        await using (var db = factory.Database.NewContext())
        {
            db.Users.Add(new ApplicationUser { Id = "admin", UserName = "admin" });
            db.Recipes.Add(Recipe(factory.CategoryId, "admin", "admin-draft", RecipeStatus.Draft, new(2026, 2, 1)));
            await db.SaveChangesAsync();
        }

        var result = await factory.Client("Admin", userId: "admin")
            .GetFromJsonAsync<PagedResult<RecipeDashboardItemDto>>("/api/v1/recipes/mine");

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("admin-draft", Assert.Single(result.Items).Slug);
    }
}
