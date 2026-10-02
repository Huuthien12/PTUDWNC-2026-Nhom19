using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class CreateRecipeTests
{
    private static JsonObject Body(RecipeApiFactory factory) => new()
    {
        ["title"] = "Canh chua cá", ["description"] = "A recipe",
        ["categoryId"] = factory.CategoryId.ToString(), ["prepTime"] = 10,
        ["cookTime"] = 20, ["servings"] = 2, ["difficulty"] = 1
    };

    [Theory]
    [InlineData("Author", true)]
    [InlineData("Admin", false)]
    public async Task Create_returns_saved_draft_with_server_fields_and_owned_nutrition(string role, bool nutrition)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client(role);
        var body = Body(factory);
        body["authorId"] = "forged-user";
        body["status"] = 2;
        body["slug"] = "forged-slug";
        body["rowVersion"] = "AQ==";
        body["steps"] = new JsonArray(new JsonObject { ["title"] = "ignored" });
        body["ingredients"] = new JsonArray(new JsonObject { ["name"] = "ignored" });
        if (nutrition)
        {
            body["instructions"] = "Nấu chín cá";
            body["nutrition"] = new JsonObject { ["calories"] = 150, ["protein"] = 12.5m, ["fat"] = 0 };
        }
        var response = await client.PostAsJsonAsync("/api/v1/recipes", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
        Assert.Equal("/api/v1/recipes/canh-chua-ca", response.Headers.Location?.ToString());
        Assert.Equal(factory.AuthorId, dto.AuthorId);
        Assert.Equal(RecipeStatus.Draft, dto.Status);
        Assert.Null(dto.PublishedAt);
        Assert.NotEmpty(Convert.FromBase64String(dto.RowVersion));
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.Include(x => x.Steps).Include(x => x.Ingredients).SingleAsync(x => x.Id == dto.Id);
        Assert.Equal(dto.RowVersion, Convert.ToBase64String(saved.RowVersion));
        Assert.Equal(dto.Title, saved.Title);
        Assert.Equal(dto.Slug, saved.Slug);
        Assert.Equal(factory.AuthorId, saved.AuthorId);
        Assert.Equal(RecipeStatus.Draft, saved.Status);
        Assert.Equal(nutrition ? "Nấu chín cá" : "", saved.Instructions);
        Assert.Equal(saved.Instructions, dto.Instructions);
        Assert.Equal(nutrition ? 150m : (decimal?)null, saved.Nutrition.Calories);
        Assert.Equal(nutrition ? 12.5m : (decimal?)null, saved.Nutrition.Protein);
        Assert.Equal(nutrition ? 0m : (decimal?)null, saved.Nutrition.Fat);
        Assert.Null(saved.Nutrition.Carbs);
        Assert.Equal(saved.Nutrition.Calories, dto.Nutrition.Calories);
        Assert.Equal(PostgreSqlDateTime.Normalize(saved.CreatedAt), PostgreSqlDateTime.Normalize(dto.CreatedAt));
        Assert.Equal(PostgreSqlDateTime.Normalize(saved.UpdatedAt), PostgreSqlDateTime.Normalize(dto.UpdatedAt));
        Assert.Empty(saved.Steps);
        Assert.Empty(saved.Ingredients);
    }

    [Theory]
    [InlineData(null, false, true, 401)]
    [InlineData("Reader", false, true, 403)]
    [InlineData("Author", true, true, 401)]
    [InlineData("Author", false, false, 401)]
    public async Task Authorization_rejects_without_writing(string? role, bool expired, bool subject, int status)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client(role, expired, subject);
        var response = await client.PostAsJsonAsync("/api/v1/recipes", Body(factory));
        Assert.Equal(status, (int)response.StatusCode);
        await using var db = factory.Database.NewContext();
        Assert.Equal(1, await db.Recipes.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_invalid_jwt_returns_problem_details_challenge(bool invalid)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client(role: null);
        if (invalid)
            client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-jwt");

        var response = await client.PostAsJsonAsync("/api/v1/recipes", Body(factory));
        Assert.Equal(401, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("AUTH_TOKEN_INVALID", (string?)problem["type"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["traceId"]));
    }

    [Theory]
    [InlineData("title", "bad")]
    [InlineData("title", "!!!!!")]
    [InlineData("categoryId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("categoryId", "11111111-1111-1111-1111-111111111111")]
    [InlineData("cookTime", "0")]
    [InlineData("prepTime", "0")]
    [InlineData("servings", "0")]
    [InlineData("difficulty", "99")]
    [InlineData("nutrition", "negative")]
    [InlineData("description", "null")]
    public async Task Invalid_input_returns_validation_problem_and_does_not_persist(string field, string value)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client();
        var body = Body(factory);
        if (field == "nutrition") body[field] = new JsonObject { ["calories"] = -1 };
        else if (field == "description") body[field] = null;
        else if (int.TryParse(value, out var number)) body[field] = number;
        else body[field] = value;
        var response = await client.PostAsJsonAsync("/api/v1/recipes", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("VALIDATION_ERROR", (string?)problem["type"]);
        Assert.NotEmpty(problem["errors"]!.AsObject());
        await using var db = factory.Database.NewContext();
        Assert.Equal(1, await db.Recipes.CountAsync());
    }

    [Theory]
    [InlineData("{ invalid json")]
    [InlineData("{\"categoryId\":\"not-a-guid\"}")]
    public async Task Malformed_json_is_400(string json)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client();
        var response = await client.PostAsync("/api/v1/recipes", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_slug_including_soft_deleted_returns_409(bool deleted)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        await using (var db = factory.Database.NewContext())
        {
            var original = await db.Recipes.SingleAsync();
            original.IsDeleted = deleted;
            await db.SaveChangesAsync();
        }
        using var client = factory.Client();
        var body = Body(factory);
        body["title"] = "Original recipe";
        var response = await client.PostAsJsonAsync("/api/v1/recipes", body);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RECIPE_SLUG_EXISTS", (string?)JsonNode.Parse(await response.Content.ReadAsStringAsync())!["type"]);
        await using var verify = factory.Database.NewContext();
        Assert.Equal(1, await verify.Recipes.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Postgres_unique_constraint_after_precheck_is_409()
    {
        await using var factory = new RecipeApiFactory { FailWithSlugConstraint = true };
        await factory.InitializeAsync();
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/v1/recipes", Body(factory));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RECIPE_SLUG_EXISTS", (string?)JsonNode.Parse(await response.Content.ReadAsStringAsync())!["type"]);
        await using var db = factory.Database.NewContext();
        Assert.Equal(1, await db.Recipes.CountAsync());
    }

    [Fact]
    public async Task Unique_index_rejects_racing_writer_and_rolls_back_entire_save()
    {
        await using var fixture = new RecipeDatabaseFixture();
        var originalId = await fixture.InitializeAsync();
        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();
        var original = await first.Recipes.SingleAsync(x => x.Id == originalId);
        var one = new RecipeRepository(first);
        var two = new RecipeRepository(second);
        Assert.False(await one.SlugExistsAsync("racing-recipe"));
        Assert.False(await two.SlugExistsAsync("racing-recipe"));
        Recipe NewRecipe(decimal calories) => new()
        {
            Title = "Racing recipe", Slug = "racing-recipe", CategoryId = original.CategoryId,
            AuthorId = original.AuthorId, Nutrition = new() { Calories = calories }
        };
        await one.AddAsync(NewRecipe(100));
        await first.SaveChangesAsync();
        await two.AddAsync(NewRecipe(999));
        var trackedOriginal = await second.Recipes.SingleAsync(x => x.Id == originalId);
        trackedOriginal.Nutrition.Calories = 999;
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        await using var verify = fixture.NewContext();
        Assert.Equal(2, await verify.Recipes.CountAsync());
        Assert.All(await verify.Recipes.ToListAsync(), x => Assert.Equal(100m, x.Nutrition.Calories));
    }
}
