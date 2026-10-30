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

    [Theory]
    [InlineData("published", null, false, true)]
    [InlineData("own-draft", "owner", false, true)]
    [InlineData("own-draft", "other", true, true)]
    [InlineData("own-draft", null, false, false)]
    [InlineData("own-draft", "other", false, false)]
    [InlineData("own-draft", "   ", false, false)]
    [InlineData("archived", "owner", false, true)]
    [InlineData("archived", "other", true, true)]
    [InlineData("archived", null, false, false)]
    [InlineData("archived", "other", false, false)]
    [InlineData("deleted", null, false, false)]
    [InlineData("deleted", "owner", false, false)]
    [InlineData("deleted", "other", true, false)]
    [InlineData("missing", null, false, false)]
    [InlineData("missing", "owner", true, false)]
    public async Task Detail_access_projection_matches_existing_visibility(
        string slug, string? userId, bool admin, bool visible)
    {
        await using var db = NewContext();
        var repository = new RecipeRepository(db);

        var access = await repository.GetDetailAccessBySlugAsync(slug, userId, admin);

        Assert.Equal(visible, access is not null);
        Assert.Empty(db.ChangeTracker.Entries());
        var detail = await repository.GetBySlugAsync(slug, userId, admin);
        Assert.Equal(detail is not null, access is not null);
        if (detail is not null)
        {
            Assert.Equal(detail.Id, access!.Id);
            Assert.Equal(detail.Slug, access.Slug);
            Assert.Equal(detail.Status, access.Status);
            Assert.Equal(detail.RowVersion, access.RowVersion);
        }
    }

    [Fact]
    public async Task Detail_access_projection_returns_the_persisted_version_without_tracking()
    {
        Guid id;
        byte[] persistedVersion;
        await using (var writer = NewContext())
        {
            var recipe = await writer.Recipes.SingleAsync(item => item.Slug == "published");
            recipe.Title = "Updated title";
            await writer.SaveChangesAsync();
            id = recipe.Id;
            persistedVersion = recipe.RowVersion.ToArray();
        }

        await using var reader = NewContext();
        var access = await new RecipeRepository(reader)
            .GetDetailAccessBySlugAsync("published", null, false);

        Assert.NotNull(access);
        Assert.Equal(id, access.Id);
        Assert.Equal("published", access.Slug);
        Assert.Equal(RecipeStatus.Published, access.Status);
        Assert.NotEmpty(persistedVersion);
        Assert.Equal(persistedVersion, access.RowVersion);
        Assert.Empty(reader.ChangeTracker.Entries());
    }
}
