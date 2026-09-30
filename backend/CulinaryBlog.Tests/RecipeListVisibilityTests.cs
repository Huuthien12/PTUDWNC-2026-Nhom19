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
        await _connection.OpenAsync(); await using var db = NewContext(); await db.Database.EnsureCreatedAsync();
        var category = new Category { Name = "Category", Slug = "category" }; db.Categories.Add(category); await db.SaveChangesAsync(); _categoryId = category.Id;
        db.Users.AddRange(new ApplicationUser { Id = "owner", UserName = "owner" }, new ApplicationUser { Id = "other", UserName = "other" }); await db.SaveChangesAsync();
        db.Recipes.AddRange(Recipe("published-old", "owner", RecipeStatus.Published, 1), Recipe("published-new", "other", RecipeStatus.Published, 2), Recipe("own-draft", "owner", RecipeStatus.Draft, 3), Recipe("other-draft", "other", RecipeStatus.Draft, 4), Recipe("archived", "owner", RecipeStatus.Archived, 5), Recipe("deleted", "owner", RecipeStatus.Published, 6, true)); await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();
    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    private Recipe Recipe(string slug, string author, RecipeStatus status, int day, bool deleted = false) => new() { Title = slug, Slug = slug, AuthorId = author, CategoryId = _categoryId, Status = status, IsDeleted = deleted, CreatedAt = new DateTime(2026, 1, day), Nutrition = new RecipeNutrition() };
    private async Task<(int Count, IReadOnlyList<string> Slugs, int Pages)> List(string? user, bool admin, int page = 1, int size = 12)
    { await using var db = NewContext(); var repo = new RecipeRepository(db); var query = new GetRecipesQueryHandler(new UnitOfWork(db, new CategoryRepository(db), repo)); var result = await query.Handle(new GetRecipesQuery(page, size, user, admin), default); return (result.TotalCount, result.Items.Select(x => x.Slug).ToList(), result.TotalPages); }
    [Fact] public async Task Guest_sees_only_published() { var r = await List(null, false); Assert.Equal(2, r.Count); Assert.Equal(["published-new", "published-old"], r.Slugs); }
    [Fact] public async Task Author_sees_published_and_own_draft_only() { var r = await List("owner", false); Assert.Equal(3, r.Count); Assert.Contains("own-draft", r.Slugs); Assert.DoesNotContain("other-draft", r.Slugs); }
    [Fact] public async Task Admin_sees_all_non_deleted_statuses() { var r = await List("admin", true); Assert.Equal(5, r.Count); Assert.DoesNotContain("deleted", r.Slugs); }
    [Fact] public async Task Pagination_uses_filtered_total_and_ordering() { var r = await List(null, false, 2, 1); Assert.Equal(2, r.Count); Assert.Equal(2, r.Pages); Assert.Equal(["published-old"], r.Slugs); }
}
