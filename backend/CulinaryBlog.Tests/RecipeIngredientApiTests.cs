using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeIngredientApiTests
{

    [Fact]
    public async Task Child_mutation_accepts_the_current_recipe_row_version()
    {
        await using var fixture = new RecipeDatabaseFixture();
        var id = await fixture.InitializeAsync();
        await using var db = fixture.NewContext();
        var recipe = await db.Recipes.SingleAsync(x => x.Id == id);
        var ingredient = new RecipeIngredient { RecipeId = id, Name = "Flour", Quantity = 2, Unit = "g" };
        recipe.Ingredients.Add(ingredient);
        db.RecipeIngredients.Add(ingredient);
        new RecipeRepository(db).UpdateForChildMutation(recipe, recipe.RowVersion.ToArray());
        await db.SaveChangesAsync();
    }
    private static async Task<Recipe> SeedAsync(RecipeApiFactory factory)
    {
        await factory.InitializeAsync();
        await using var db = factory.Database.NewContext();
        return await db.Recipes.AsNoTracking().SingleAsync();
    }

    private static object Body(string rowVersion, string name = "Flour") => new
    {
        name, quantity = 2m, unit = "g", notes = "Fine", sortOrder = 1, rowVersion
    };

    private static async Task AssertProblem(HttpResponseMessage response, int status, string type)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(type, (string?)body["type"]);
    }

    [Fact]
    public async Task Owner_can_create_update_and_soft_delete_with_rotated_row_versions()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var client = factory.Client();
        var first = await client.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients", Body(Convert.ToBase64String(recipe.RowVersion)));
        Assert.True(first.StatusCode == HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        var created = (await first.Content.ReadFromJsonAsync<RecipeIngredientDto>())!;
        var update = await client.PutAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients/{created.Id}", Body(created.RowVersion, "Whole flour"));
        var updated = (await update.Content.ReadFromJsonAsync<RecipeIngredientDto>())!;
        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/recipes/{recipe.Id}/ingredients/{created.Id}") { Content = JsonContent.Create(new { rowVersion = updated.RowVersion }) });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.NotEqual(created.RowVersion, updated.RowVersion);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.NotEqual($"\"{updated.RowVersion}\"", delete.Headers.ETag?.Tag);
        await using var verify = factory.Database.NewContext();
        Assert.True((await verify.RecipeIngredients.IgnoreQueryFilters().SingleAsync(x => x.Id == created.Id)).IsDeleted);
    }

    [Fact]
    public async Task Ingredient_mutations_enforce_auth_validation_and_not_found()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        var version = Convert.ToBase64String(recipe.RowVersion);
        using var guest = factory.Client(role: null);
        using var other = factory.Client(userId: "other-author");
        using var owner = factory.Client();

        await AssertProblem(await guest.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients", Body(version)), 401, "AUTH_TOKEN_INVALID");
        await AssertProblem(await other.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients", Body(version)), 403, "RECIPE_FORBIDDEN");
        await AssertProblem(await owner.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients", Body(version, "")), 400, "VALIDATION_ERROR");
        await AssertProblem(await owner.PutAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients/{Guid.NewGuid()}", Body(version)), 404, "RECIPE_INGREDIENT_NOT_FOUND");
    }

    [Fact]
    public async Task Stale_and_malformed_ingredient_row_versions_are_rejected()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var client = factory.Client();
        var original = Convert.ToBase64String(recipe.RowVersion);

        var first = await client.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients", Body(original));
        Assert.True(first.StatusCode == HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        await AssertProblem(await client.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients", Body(original)), 422, "RECIPE_CONCURRENCY_CONFLICT");
        await AssertProblem(await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/recipes/{recipe.Id}/ingredients/{Guid.NewGuid()}") { Content = JsonContent.Create(new { rowVersion = "not-base64" }) }), 400, "VALIDATION_ERROR");
    }
}
