using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CulinaryBlog.Tests;

// Dedicated relational fixture: keeps the complete production Recipe model and save pipeline.
public sealed class RecipeDatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string? _postgresConnection = CreatePostgresConnection();

    private static string? CreatePostgresConnection()
    {
        var configured = Environment.GetEnvironmentVariable("RECIPE_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return null;
        return new Npgsql.NpgsqlConnectionStringBuilder(configured)
        {
            Database = $"recipe_tests_{Guid.NewGuid():N}", Pooling = false
        }.ConnectionString;
    }

    public AppDbContext NewContext() => new(_postgresConnection is null
        ? new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options
        : new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgresConnection).Options);

    public async Task<Guid> InitializeAsync(bool beforeVersionMigration = false)
    {
        await _connection.OpenAsync();
        await using var context = NewContext();
        if (_postgresConnection is not null)
            await context.GetService<IMigrator>().MigrateAsync(beforeVersionMigration
                ? "20260927062905_AddRefreshTokenRevokedAt" : null);
        else
            await context.Database.EnsureCreatedAsync();
        var recipe = new Recipe
        {
            Title = "Original recipe", Slug = "original-recipe",
            Category = new Category { Name = "Test category", Slug = "test-category" },
            Author = new ApplicationUser { UserName = "recipe-test" },
            Nutrition = new RecipeNutrition { Calories = 100 }
        };
        await new RecipeRepository(context).AddAsync(recipe);
        await context.SaveChangesAsync();
        return recipe.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgresConnection is not null)
        {
            await using var context = NewContext();
            // Only the randomly named database owned by this fixture is dropped.
            await context.Database.EnsureDeletedAsync();
        }
        await _connection.DisposeAsync();
    }
}
