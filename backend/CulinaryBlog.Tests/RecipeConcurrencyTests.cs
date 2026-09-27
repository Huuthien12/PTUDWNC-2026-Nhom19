using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeConcurrencyTests
{
    [PostgresFact]
    public async Task Migration_backfills_only_empty_tokens_without_changing_recipe_content()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync(beforeVersionMigration: true);
        await using var context = database.NewContext();
        var existing = await context.Recipes.SingleAsync();
        var retainedVersion = existing.RowVersion.ToArray();
        var legacy = new Recipe
        {
            Title = "Legacy recipe", Slug = "legacy-recipe", AuthorId = existing.AuthorId,
            CategoryId = existing.CategoryId, Nutrition = new RecipeNutrition { Calories = 321 }
        };
        context.Recipes.Add(legacy);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Recipes\" SET \"RowVersion\" = {Array.Empty<byte>()} WHERE \"Id\" = {legacy.Id}");
        const string contents = """
            SELECT (to_jsonb(r) - 'RowVersion')::text AS "Value" FROM "Recipes" r ORDER BY "Id"
            """;
        var before = await context.Database.SqlQueryRaw<string>(contents).ToListAsync();
        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(before, await context.Database.SqlQueryRaw<string>(contents).ToListAsync());
        Assert.Equal(retainedVersion, (await context.Recipes.SingleAsync(r => r.Id == id)).RowVersion);
        Assert.Equal(16, (await context.Recipes.SingleAsync(r => r.Id == legacy.Id)).RowVersion.Length);
        Assert.Equal(2, await context.Recipes.CountAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Two_contexts_reject_stale_writes_including_nutrition_only(
        bool firstNutritionOnly, bool secondNutritionOnly)
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        await using var first = database.NewContext();
        await using var second = database.NewContext();
        var winner = await first.Recipes.SingleAsync(r => r.Id == id);
        var loser = await second.Recipes.SingleAsync(r => r.Id == id);
        var oldVersion = winner.RowVersion.ToArray();
        Assert.NotEmpty(oldVersion);
        Assert.Equal(oldVersion, loser.RowVersion);

        if (firstNutritionOnly) winner.Nutrition.Calories = 200;
        else winner.Title = "Winner";
        if (secondNutritionOnly) loser.Nutrition.Calories = 300;
        else loser.Title = "Loser";

        await first.SaveChangesAsync();
        Assert.False(oldVersion.SequenceEqual(winner.RowVersion));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var verify = database.NewContext();
        var stored = await verify.Recipes.SingleAsync();
        Assert.Equal(winner.RowVersion, stored.RowVersion);
        Assert.Equal(firstNutritionOnly ? "Original recipe" : "Winner", stored.Title);
        Assert.Equal(firstNutritionOnly ? 200m : 100m, stored.Nutrition.Calories);
    }

    [Fact]
    public async Task Repository_checks_client_version_even_after_loading_latest_row()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        byte[] clientVersion;
        await using (var first = database.NewContext())
        {
            var recipe = await first.Recipes.SingleAsync();
            clientVersion = recipe.RowVersion.ToArray();
            recipe.Title = "Already saved";
            await first.SaveChangesAsync();
        }
        await using var second = database.NewContext();
        var repository = new RecipeRepository(second);
        var latest = (await repository.GetByIdAsync(id))!;
        latest.Nutrition.Protein = 42;
        repository.Update(latest, clientVersion);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verify = database.NewContext();
        var stored = await verify.Recipes.SingleAsync();
        Assert.Equal("Already saved", stored.Title);
        Assert.Null(stored.Nutrition.Protein);
    }

    [Fact]
    public async Task Sync_save_replacement_nutrition_and_noop_preserve_expected_versions()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        using var context = database.NewContext();
        var repository = new RecipeRepository(context);
        var recipe = (await repository.GetByIdAsync(id))!;
        var version = recipe.RowVersion.ToArray();
        Assert.Equal(0, context.SaveChanges());
        Assert.Equal(version, recipe.RowVersion);
        recipe.Nutrition = new RecipeNutrition { Fat = 12 };
        repository.Update(recipe, version);
        context.SaveChanges();
        Assert.False(version.SequenceEqual(recipe.RowVersion));
        var next = recipe.RowVersion.ToArray();
        recipe.Nutrition.Carbs = 15;
        context.SaveChanges();
        Assert.False(next.SequenceEqual(recipe.RowVersion));
        await using var verify = database.NewContext();
        var stored = await verify.Recipes.SingleAsync();
        Assert.Equal(12m, stored.Nutrition.Fat);
        Assert.Equal(15m, stored.Nutrition.Carbs);
        Assert.Equal(recipe.RowVersion, stored.RowVersion);
    }

    [Fact]
    public async Task Legacy_empty_token_upgrades_on_first_write_and_then_rejects_stale_write()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        await using var first = database.NewContext();
        await first.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Recipes\" SET \"RowVersion\" = {Array.Empty<byte>()}");
        await using var second = database.NewContext();
        var winner = await first.Recipes.SingleAsync();
        var loser = await second.Recipes.SingleAsync();
        Assert.Empty(winner.RowVersion);
        winner.Nutrition.Protein = 10;
        await first.SaveChangesAsync();
        Assert.NotEmpty(winner.RowVersion);
        loser.Title = "Stale legacy write";
        new RecipeRepository(second).Update(loser, []);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Other_entities_keep_their_versions_and_slug_checks_include_deleted_rows()
    {
        await using var database = new RecipeDatabaseFixture();
        var id = await database.InitializeAsync();
        await using var context = database.NewContext();
        var category = await context.Categories.SingleAsync();
        Assert.Empty(category.RowVersion);
        category.Name = "Renamed";
        await context.SaveChangesAsync();
        Assert.Empty(category.RowVersion);
        var recipe = await context.Recipes.SingleAsync();
        recipe.IsDeleted = true;
        await context.SaveChangesAsync();
        var repository = new RecipeRepository(context);
        Assert.True(await repository.SlugExistsAsync(recipe.Slug));
        Assert.False(await repository.SlugExistsAsync(recipe.Slug, id));
        await using var reader = database.NewContext();
        Assert.Null(await new RecipeRepository(reader).GetByIdAsync(id));
    }

    [Fact]
    public void PostgreSql_model_has_no_pending_schema_changes()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        Assert.False(context.Database.HasPendingModelChanges());
        var model = context.GetService<IDesignTimeModel>().Model;
        var version = model.FindEntityType(typeof(Recipe))!.FindProperty(nameof(Recipe.RowVersion))!;
        Assert.Equal("bytea", version.GetColumnType());
        Assert.True(version.IsConcurrencyToken);
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECIPE_TEST_POSTGRES")))
            Skip = "Set RECIPE_TEST_POSTGRES to run against an isolated PostgreSQL test server.";
    }
}
