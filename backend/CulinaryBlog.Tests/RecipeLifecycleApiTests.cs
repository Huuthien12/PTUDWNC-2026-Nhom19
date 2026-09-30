using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeLifecycleApiTests
{
    private static async Task<Recipe> SeedAsync(RecipeApiFactory factory, RecipeStatus status = RecipeStatus.Draft)
    {
        await factory.InitializeAsync();
        await using var db = factory.Database.NewContext();
        var recipe = await db.Recipes.SingleAsync();
        recipe.Status = status;
        recipe.PublishedAt = status == RecipeStatus.Published ? DateTime.UtcNow.AddDays(-2) : null;
        recipe.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Salt", Quantity = 1, Unit = "g", OrderIndex = 1 });
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });
        db.RecipeIngredients.AddRange(recipe.Ingredients);
        db.RecipeSteps.AddRange(recipe.Steps);
        await db.SaveChangesAsync();
        return recipe;
    }

    private static object Body(Recipe recipe) => new { rowVersion = Convert.ToBase64String(recipe.RowVersion) };

    private static async Task AssertProblem(HttpResponseMessage response, int status, string type)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(type, (string?)body["type"]);
        Assert.Equal(status, (int?)body["status"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)body["traceId"]));
    }

    [Fact]
    public async Task Lifecycle_repository_loads_ingredients_and_steps_for_publish_rules()
    {
        await using var factory = new RecipeApiFactory();
        var seeded = await SeedAsync(factory);
        await using var db = factory.Database.NewContext();

        var recipe = await new RecipeRepository(db).GetForLifecycleAsync(seeded.Id);

        Assert.NotNull(recipe);
        Assert.Single(recipe.Ingredients);
        Assert.Single(recipe.Steps);
    }

    [Fact]
    public async Task Guest_publish_is_rfc7807_401()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var client = factory.Client(role: null);

        await AssertProblem(await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe)), 401, "AUTH_TOKEN_INVALID");
    }

    [Theory]
    [InlineData("publish", RecipeStatus.Draft, RecipeStatus.Published)]
    [InlineData("unpublish", RecipeStatus.Published, RecipeStatus.Draft)]
    [InlineData("archive", RecipeStatus.Draft, RecipeStatus.Archived)]
    public async Task Owner_can_apply_each_mapped_lifecycle_action(string route, RecipeStatus beforeStatus, RecipeStatus expectedStatus)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, beforeStatus);
        using var client = factory.Client();

        var response = await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/{route}", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
        Assert.Equal(expectedStatus, dto.Status);
        Assert.NotEqual(Convert.ToBase64String(recipe.RowVersion), dto.RowVersion);
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    public async Task Other_author_is_forbidden_and_missing_recipe_is_not_found(string route)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, route == "unpublish" ? RecipeStatus.Published : RecipeStatus.Draft);
        using var otherAuthor = factory.Client(userId: "other-author");
        using var owner = factory.Client();

        await AssertProblem(await otherAuthor.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/{route}", Body(recipe)), 403, "RECIPE_FORBIDDEN");
        await AssertProblem(await owner.PatchAsJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/{route}", Body(recipe)), 404, "RECIPE_NOT_FOUND");
    }

    [Fact]
    public async Task Admin_can_archive_and_archive_is_idempotent()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var admin = factory.Client("Admin", userId: "admin-user");

        var first = await admin.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/archive", Body(recipe));
        var archived = (await first.Content.ReadFromJsonAsync<RecipeDto>())!;
        var second = await admin.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/archive", new { rowVersion = archived.RowVersion });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var repeated = (await second.Content.ReadFromJsonAsync<RecipeDto>())!;
        Assert.Equal(RecipeStatus.Archived, repeated.Status);
        Assert.NotEmpty(Convert.FromBase64String(repeated.RowVersion));
    }

    [Fact]
    public async Task Admin_can_publish_another_authors_complete_draft()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var admin = factory.Client("Admin", userId: "admin-user");

        var response = await admin.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(RecipeStatus.Published, (await response.Content.ReadFromJsonAsync<RecipeDto>())!.Status);
    }

    [Fact]
    public async Task Soft_deleted_recipe_is_not_found_for_lifecycle_actions()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        await using (var db = factory.Database.NewContext())
        {
            (await db.Recipes.SingleAsync(x => x.Id == recipe.Id)).IsDeleted = true;
            await db.SaveChangesAsync();
        }
        using var admin = factory.Client("Admin", userId: "admin-user");

        await AssertProblem(await admin.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/archive", Body(recipe)), 404, "RECIPE_NOT_FOUND");
    }

    [Fact]
    public async Task Stale_lifecycle_row_version_returns_rfc7807_422()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var client = factory.Client();

        var winner = await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe));
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        var current = (await winner.Content.ReadFromJsonAsync<RecipeDto>())!;
        await AssertProblem(await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/unpublish", Body(recipe)), 422, "RECIPE_CONCURRENCY_CONFLICT");

        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Published, saved.Status);
        Assert.Equal(current.RowVersion, Convert.ToBase64String(saved.RowVersion));
    }
}
