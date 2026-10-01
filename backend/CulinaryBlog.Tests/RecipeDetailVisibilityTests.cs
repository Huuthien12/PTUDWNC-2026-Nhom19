using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeDetailVisibilityTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private Guid _categoryId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();
        var category = new Category { Name = "Category", Slug = "category" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        _categoryId = category.Id;
        db.Users.AddRange(
            new ApplicationUser { Id = "owner", UserName = "owner" },
            new ApplicationUser { Id = "other", UserName = "other" });
        await db.SaveChangesAsync();
        db.Recipes.AddRange(
            Recipe("published", "owner", RecipeStatus.Published),
            Recipe("own-draft", "owner", RecipeStatus.Draft),
            Recipe("other-draft", "other", RecipeStatus.Draft),
            Recipe("archived", "owner", RecipeStatus.Archived),
            Recipe("deleted", "owner", RecipeStatus.Published, true));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();
    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    private Recipe Recipe(string slug, string author, RecipeStatus status, bool deleted = false) => new()
    {
        Title = slug, Slug = slug, AuthorId = author, CategoryId = _categoryId, Status = status, IsDeleted = deleted
    };

    [Theory]
    [InlineData("published", null, false, true)]
    [InlineData("own-draft", "owner", false, true)]
    [InlineData("other-draft", "owner", false, false)]
    [InlineData("archived", "owner", false, true)]
    [InlineData("archived", "other", false, false)]
    [InlineData("archived", null, false, false)]
    [InlineData("deleted", "owner", true, false)]
    [InlineData("other-draft", null, true, true)]
    public async Task Detail_visibility_matches_actor_scope(string slug, string? userId, bool admin, bool visible)
    {
        await using var db = NewContext();
        var recipe = await new RecipeRepository(db).GetBySlugAsync(slug, userId, admin);
        Assert.Equal(visible, recipe is not null);
    }
}
