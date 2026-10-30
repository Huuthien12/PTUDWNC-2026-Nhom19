using CulinaryBlog.Application.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeListVisibilityTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private Guid _categoryId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var db = NewContext();

        await db.Database.EnsureCreatedAsync();

        var category = new Category
        {
            Name = "Category",
            Slug = "category"
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync();

        _categoryId = category.Id;

        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner",
                UserName = "owner"
            },
            new ApplicationUser
            {
                Id = "other",
                UserName = "other"
            });

        await db.SaveChangesAsync();

        db.Recipes.AddRange(
            Recipe(
                "published-old",
                "owner",
                RecipeStatus.Published,
                1,
                cookTime: 20,
                servings: 2,
                difficulty: RecipeDifficulty.Easy),

            Recipe(
                "published-new",
                "other",
                RecipeStatus.Published,
                2,
                cookTime: 40,
                servings: 4,
                difficulty: RecipeDifficulty.Medium),

            Recipe(
                "own-draft",
                "owner",
                RecipeStatus.Draft,
                3,
                cookTime: 30,
                servings: 3,
                difficulty: RecipeDifficulty.Easy),

            Recipe(
                "other-draft",
                "other",
                RecipeStatus.Draft,
                4,
                cookTime: 50,
                servings: 6,
                difficulty: RecipeDifficulty.Hard),

            Recipe(
                "archived",
                "owner",
                RecipeStatus.Archived,
                5,
                cookTime: 60,
                servings: 8,
                difficulty: RecipeDifficulty.Hard),

            Recipe(
                "deleted",
                "owner",
                RecipeStatus.Published,
                6,
                deleted: true));

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
        => _connection.DisposeAsync().AsTask();

    private AppDbContext NewContext()
        => new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);

    private Recipe Recipe(
        string slug,
        string author,
        RecipeStatus status,
        int day,
        int cookTime = 30,
        int servings = 2,
        RecipeDifficulty difficulty = RecipeDifficulty.Easy,
        bool deleted = false)
    {
        return new Recipe
        {
            Title = slug,
            Slug = slug,
            AuthorId = author,
            CategoryId = _categoryId,
            Status = status,
            IsDeleted = deleted,
            CreatedAt = new DateTime(2026, 1, day),
            CookTime = cookTime,
            Servings = servings,
            Difficulty = difficulty,
            Nutrition = new RecipeNutrition()
        };
    }

    private async Task<(int Count, IReadOnlyList<string> Slugs, int Pages)> List(
        string? user,
        bool admin,
        int page = 1,
        int size = 12,
        Guid? categoryId = null,
        RecipeDifficulty? difficulty = null,
        int? maxCookTime = null,
        int? minServings = null,
        string sortBy = "createdAt",
        string sortOrder = "desc")
    {
        await using var db = NewContext();

        var repo = new RecipeRepository(db);

        var query = new GetRecipesQueryHandler(
            new UnitOfWork(
                db,
                new CategoryRepository(db),
                repo));

        var result = await query.Handle(
            new GetRecipesQuery(
                page,
                size,
                user,
                admin,
                categoryId,
                difficulty,
                maxCookTime,
                minServings,
                sortBy,
                sortOrder),
            default);

        return (
            result.TotalCount,
            result.Items.Select(x => x.Slug).ToList(),
            result.TotalPages);
    }

    [Fact]
    public async Task Guest_sees_only_published()
    {
        var r = await List(null, false);

        Assert.Equal(2, r.Count);
        Assert.Equal(
            ["published-new", "published-old"],
            r.Slugs);

        Assert.DoesNotContain("archived", r.Slugs);
    }

    [Fact]
    public async Task Author_sees_published_and_own_private_statuses_only()
    {
        var r = await List("owner", false);

        Assert.Equal(4, r.Count);
        Assert.Contains("own-draft", r.Slugs);
        Assert.Contains("archived", r.Slugs);
        Assert.DoesNotContain("other-draft", r.Slugs);
    }

    [Fact]
    public async Task Admin_sees_all_non_deleted_statuses()
    {
        var r = await List("admin", true);

        Assert.Equal(5, r.Count);
        Assert.DoesNotContain("deleted", r.Slugs);
    }

    [Fact]
    public async Task Pagination_uses_filtered_total_and_ordering()
    {
        var r = await List(
            null,
            false,
            page: 2,
            size: 1);

        Assert.Equal(2, r.Count);
        Assert.Equal(2, r.Pages);
        Assert.Equal(
            ["published-old"],
            r.Slugs);
    }

    [Fact]
    public async Task Filter_by_category_returns_matching_recipes()
    {
        var r = await List(
            null,
            false,
            categoryId: _categoryId);

        Assert.Equal(2, r.Count);

        Assert.Equal(
            ["published-new", "published-old"],
            r.Slugs);
    }

    [Fact]
    public async Task Filter_by_difficulty_returns_matching_recipes()
    {
        var r = await List(
            null,
            false,
            difficulty: RecipeDifficulty.Easy);

        Assert.Equal(1, r.Count);

        Assert.Equal(
            ["published-old"],
            r.Slugs);
    }

    [Fact]
    public async Task Filter_by_max_cook_time_returns_matching_recipes()
    {
        var r = await List(
            null,
            false,
            maxCookTime: 30);

        Assert.Equal(1, r.Count);

        Assert.Equal(
            ["published-old"],
            r.Slugs);
    }

    [Fact]
    public async Task Filter_by_min_servings_returns_matching_recipes()
    {
        var r = await List(
            null,
            false,
            minServings: 4);

        Assert.Equal(1, r.Count);

        Assert.Equal(
            ["published-new"],
            r.Slugs);
    }

    [Fact]
    public async Task Multiple_filters_are_combined_with_and()
    {
        var r = await List(
            null,
            false,
            difficulty: RecipeDifficulty.Easy,
            maxCookTime: 30,
            minServings: 2);

        Assert.Equal(1, r.Count);

        Assert.Equal(
            ["published-old"],
            r.Slugs);
    }

    [Fact]
    public async Task Sort_by_title_ascending()
    {
        var r = await List(
            null,
            false,
            sortBy: "title",
            sortOrder: "asc");

        Assert.Equal(
            ["published-new", "published-old"],
            r.Slugs);
    }

    [Fact]
    public async Task Sort_by_title_descending()
    {
        var r = await List(
            null,
            false,
            sortBy: "title",
            sortOrder: "desc");

        Assert.Equal(
            ["published-old", "published-new"],
            r.Slugs);
    }

    [Fact]
    public async Task Sort_by_created_at_ascending()
    {
        var r = await List(
            null,
            false,
            sortBy: "createdAt",
            sortOrder: "asc");

        Assert.Equal(
            ["published-old", "published-new"],
            r.Slugs);
    }

    [Fact]
    public async Task Sort_by_created_at_descending()
    {
        var r = await List(
            null,
            false,
            sortBy: "createdAt",
            sortOrder: "desc");

        Assert.Equal(
            ["published-new", "published-old"],
            r.Slugs);
    }
}