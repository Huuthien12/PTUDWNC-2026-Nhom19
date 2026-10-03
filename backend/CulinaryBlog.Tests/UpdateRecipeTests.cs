using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Validators;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class UpdateRecipeTests
{
    private static async Task<Recipe> Seed(RecipeApiFactory factory, RecipeStatus status = RecipeStatus.Draft)
    {
        await factory.InitializeAsync();
        await using var db = factory.Database.NewContext();
        var recipe = await db.Recipes.SingleAsync();
        recipe.PrepTime = 10;
        recipe.CookTime = 20;
        recipe.Servings = 2;
        recipe.Difficulty = RecipeDifficulty.Easy;
        recipe.Status = status;
        recipe.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        recipe.PublishedAt = status == RecipeStatus.Published ? DateTime.UtcNow.AddDays(-2) : null;
        recipe.Steps.Add(new() { StepNumber = 1, Title = "Original step", Description = "Keep step" });
        recipe.Ingredients.Add(new() { Name = "Salt", Quantity = 1, Unit = "g" });
        recipe.Images.Add(new() { OriginalUrl = "https://example.test/original.jpg", IsPrimary = true });
        // Child IDs are assigned by BaseEntity; explicitly mark these new rows Added.
        db.RecipeSteps.AddRange(recipe.Steps);
        db.RecipeIngredients.AddRange(recipe.Ingredients);
        db.RecipeImages.AddRange(recipe.Images);
        await db.SaveChangesAsync();
        return recipe;
    }

    private static JsonObject Body(Recipe recipe) => new()
    {
        ["title"] = recipe.Title, ["description"] = recipe.Description,
        ["categoryId"] = recipe.CategoryId.ToString(), ["prepTime"] = recipe.PrepTime,
        ["cookTime"] = recipe.CookTime, ["servings"] = recipe.Servings,
        ["difficulty"] = (int)recipe.Difficulty, ["instructions"] = recipe.Instructions,
        ["rowVersion"] = Convert.ToBase64String(recipe.RowVersion)
    };

    private static async Task AssertProblem(HttpResponseMessage response, int status, string type)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(type, (string?)body["type"]);
        Assert.Equal(status, (int?)body["status"]);
    }

    [Theory]
    [InlineData("Author", RecipeStatus.Draft)]
    [InlineData("Admin", RecipeStatus.Published)]
    [InlineData("Author", RecipeStatus.Archived)]
    public async Task Owner_or_admin_updates_allowed_fields_and_preserves_identity_lifecycle_children(string role, RecipeStatus status)
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory, status);
        var nextCategory = new Category { Name = "Second category", Slug = "second-category" };
        await using (var db = factory.Database.NewContext())
        {
            db.Categories.Add(nextCategory);
            // Title collision must not affect this update because slug stays stable.
            db.Recipes.Add(new Recipe { Title = "Existing title", Slug = "existing-title",
                AuthorId = before.AuthorId, CategoryId = before.CategoryId });
            await db.SaveChangesAsync();
        }
        using var client = factory.Client(role, userId: role == "Admin" ? "another-admin" : null);
        var body = Body(before);
        body["title"] = "Existing title";
        body["description"] = "Updated description";
        body["instructions"] = "Updated instructions";
        body["categoryId"] = nextCategory.Id.ToString();
        body["prepTime"] = 5;
        body["cookTime"] = 0;
        body["servings"] = 3;
        body["difficulty"] = 3;
        body["nutrition"] = new JsonObject { ["protein"] = 12.5m, ["fat"] = 0 };
        body["id"] = Guid.NewGuid().ToString();
        body["authorId"] = "forged";
        body["status"] = 99;
        body["slug"] = "forged";
        body["publishedAt"] = "2020-01-01T00:00:00Z";
        body["steps"] = new JsonArray();
        body["ingredients"] = new JsonArray();
        body["images"] = new JsonArray();
        var response = await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
        await using var verify = factory.Database.NewContext();
        var saved = await verify.Recipes.Include(x => x.Steps).Include(x => x.Ingredients)
            .Include(x => x.Images).SingleAsync(x => x.Id == before.Id);
        Assert.Equal(before.Id, dto.Id);
        Assert.Equal(before.AuthorId, saved.AuthorId);
        Assert.Equal(before.AuthorId, dto.AuthorId);
        Assert.Equal(status, saved.Status);
        Assert.Equal(status, dto.Status);
        Assert.Equal(before.Slug, saved.Slug);
        Assert.Equal(before.Slug, dto.Slug);
        Assert.Equal(PostgreSqlDateTime.Normalize(before.PublishedAt), PostgreSqlDateTime.Normalize(saved.PublishedAt));
        Assert.Equal(PostgreSqlDateTime.Normalize(before.CreatedAt), PostgreSqlDateTime.Normalize(saved.CreatedAt));
        Assert.True(saved.UpdatedAt > before.UpdatedAt);
        Assert.Equal(PostgreSqlDateTime.Normalize(saved.UpdatedAt), PostgreSqlDateTime.Normalize(dto.UpdatedAt));
        Assert.Equal("Existing title", saved.Title);
        Assert.Equal("Updated description", saved.Description);
        Assert.Equal("Updated instructions", saved.Instructions);
        Assert.Equal(nextCategory.Id, saved.CategoryId);
        Assert.Equal(5, saved.PrepTime);
        Assert.Equal(0, saved.CookTime);
        Assert.Equal(3, saved.Servings);
        Assert.Equal(RecipeDifficulty.Hard, saved.Difficulty);
        Assert.Null(saved.Nutrition.Calories);
        Assert.Equal(12.5m, saved.Nutrition.Protein);
        Assert.Equal(0m, saved.Nutrition.Fat);
        Assert.Equal(before.Steps.Single().Id, Assert.Single(saved.Steps).Id);
        Assert.Equal("Keep step", saved.Steps.Single().Description);
        Assert.Equal(before.Ingredients.Single().Id, Assert.Single(saved.Ingredients).Id);
        Assert.Equal("Salt", saved.Ingredients.Single().Name);
        Assert.Equal(before.Images.Single().Id, Assert.Single(saved.Images).Id);
        Assert.True(saved.Images.Single().IsPrimary);
        Assert.Equal(Convert.ToBase64String(saved.RowVersion), dto.RowVersion);
        Assert.NotEqual(Convert.ToBase64String(before.RowVersion), dto.RowVersion);
        if (status == RecipeStatus.Published) Assert.Equal("categories:all", Assert.Single(factory.Cache.RemovedKeys));
        else Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Theory]
    [InlineData(null, null, 401)]
    [InlineData("Reader", null, 403)]
    [InlineData("Author", "other-author", 403)]
    public async Task Unauthorized_updates_do_not_write(string? role, string? userId, int status)
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory);
        using var client = factory.Client(role, userId: userId);
        var body = Body(before);
        body["title"] = "Changed title";
        var response = await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", body);
        Assert.Equal(status, (int)response.StatusCode);
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync();
        Assert.Equal(before.Title, saved.Title);
        Assert.Equal(before.RowVersion, saved.RowVersion);
        Assert.Equal(PostgreSqlDateTime.Normalize(before.UpdatedAt), PostgreSqlDateTime.Normalize(saved.UpdatedAt));
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Fact]
    public async Task Resource_permission_precedes_validation_and_tracked_mutation()
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory);
        await using var db = factory.Database.NewContext();
        var uow = new UnitOfWork(db, new CategoryRepository(db), new RecipeRepository(db));
        var handler = new UpdateRecipeCommandHandler(uow, new UpdateRecipeRequestValidator(), factory.Cache, TimeProvider.System);
        var invalid = new UpdateRecipeRequest("bad", "", Guid.NewGuid(), 0, -1, 0, 0, "", "invalid");
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new(before.Id, invalid, "other-author", false), default));
        Assert.Equal("RECIPE_FORBIDDEN", exception.ErrorCode);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(before.Title, db.Recipes.Local.Single().Title);
        using var client = factory.Client(userId: "other-author");
        await AssertProblem(await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", invalid), 403, "RECIPE_FORBIDDEN");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_soft_deleted_is_404(bool deleted)
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory);
        if (deleted)
        {
            await using var db = factory.Database.NewContext();
            (await db.Recipes.SingleAsync()).IsDeleted = true;
            await db.SaveChangesAsync();
        }
        using var client = factory.Client("Admin");
        await AssertProblem(await client.PutAsJsonAsync($"/api/v1/recipes/{(deleted ? before.Id : Guid.NewGuid())}", Body(before)),
            404, "RECIPE_NOT_FOUND");
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("values")]
    public async Task Nutrition_semantics_and_nutrition_only_update_rotate_version(string mode)
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory, RecipeStatus.Published);
        using var client = factory.Client();
        var body = Body(before);
        if (mode == "null") body["nutrition"] = null;
        if (mode == "empty") body["nutrition"] = new JsonObject();
        if (mode == "values") body["nutrition"] = new JsonObject { ["calories"] = 0, ["carbs"] = 12.5m };
        var response = await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync();
        Assert.Equal(mode is "omitted" or "null" ? 100m : mode == "values" ? 0m : (decimal?)null, saved.Nutrition.Calories);
        Assert.Equal(mode == "values" ? 12.5m : (decimal?)null, saved.Nutrition.Carbs);
        Assert.Equal(saved.Nutrition.Calories, dto.Nutrition.Calories);
        Assert.Equal(saved.Title, before.Title);
        Assert.Equal(saved.Status, before.Status);
        Assert.Equal(saved.CategoryId, before.CategoryId);
        Assert.Equal(Convert.ToBase64String(saved.RowVersion), dto.RowVersion);
        Assert.NotEqual(Convert.ToBase64String(before.RowVersion), dto.RowVersion);
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Fact]
    public async Task Stale_update_rolls_back_root_and_nutrition_and_does_not_invalidate_cache()
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory, RecipeStatus.Published);
        using var client = factory.Client();
        var winner = Body(before);
        winner["nutrition"] = new JsonObject { ["calories"] = 200 };
        var first = await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", winner);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var current = (await first.Content.ReadFromJsonAsync<RecipeDto>())!;
        var category = new Category { Name = "Moved", Slug = "moved" };
        await using (var db = factory.Database.NewContext())
        {
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }
        var stale = Body(before);
        stale["categoryId"] = category.Id.ToString();
        stale["title"] = "Losing update";
        stale["nutrition"] = new JsonObject { ["calories"] = 999 };
        await AssertProblem(await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", stale), 422, "RECIPE_CONCURRENCY_CONFLICT");
        await using var verify = factory.Database.NewContext();
        var saved = await verify.Recipes.SingleAsync();
        Assert.Equal(before.Title, saved.Title);
        Assert.Equal(before.CategoryId, saved.CategoryId);
        Assert.Equal(200m, saved.Nutrition.Calories);
        Assert.Equal(current.RowVersion, Convert.ToBase64String(saved.RowVersion));
        Assert.Equal(PostgreSqlDateTime.Normalize(current.UpdatedAt), PostgreSqlDateTime.Normalize(saved.UpdatedAt));
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Theory]
    [InlineData("rowVersion", "missing")]
    [InlineData("rowVersion", "not-base64")]
    [InlineData("instructions", "missing")]
    [InlineData("title", "bad")]
    [InlineData("cookTime", "-1")]
    [InlineData("categoryId", "11111111-1111-1111-1111-111111111111")]
    [InlineData("nutrition", "negative")]
    public async Task Validation_400_leaves_persistence_unchanged(string field, string value)
    {
        await using var factory = new RecipeApiFactory();
        var before = await Seed(factory);
        using var client = factory.Client();
        var body = Body(before);
        if (value == "missing") body.Remove(field);
        else if (field == "cookTime") body[field] = -1;
        else if (field == "nutrition") body[field] = new JsonObject { ["fat"] = -1 };
        else body[field] = value;
        await AssertProblem(await client.PutAsJsonAsync($"/api/v1/recipes/{before.Id}", body), 400, "VALIDATION_ERROR");
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync();
        Assert.Equal(before.RowVersion, saved.RowVersion);
        Assert.Equal(before.Title, saved.Title);
        Assert.Equal(before.Nutrition.Calories, saved.Nutrition.Calories);
        Assert.Empty(factory.Cache.RemovedKeys);
    }
}
