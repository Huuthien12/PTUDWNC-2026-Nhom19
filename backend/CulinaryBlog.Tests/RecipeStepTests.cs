using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Tests;

public sealed class RecipeStepTests : IAsyncLifetime
{
    private readonly RecipeApiFactory _factory = new();

    public Task InitializeAsync()
        => _factory.InitializeAsync();

    public async Task DisposeAsync()
        => await _factory.DisposeAsync();

    [Fact]
    public async Task Create_step_as_owner_returns_created_steps_and_new_row_version()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();
        var originalRowVersion = await GetRecipeRowVersion(recipeId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps",
            new
            {
                description = "Boil the water",
                durationMinutes = 5,
                imageUrl = (string?)null,
                rowVersion = originalRowVersion
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<RecipeStepListResponse>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(1, result.Items[0].StepNumber);
        Assert.Equal("Boil the water", result.Items[0].Description);
        Assert.Equal(5, result.Items[0].TimerMinutes);
        Assert.False(string.IsNullOrWhiteSpace(result.RowVersion));
        Assert.NotEqual(originalRowVersion, result.RowVersion);
    }

    [Fact]
    public async Task Create_second_step_assigns_next_step_number()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();

        var rowVersion = await GetRecipeRowVersion(recipeId);

        var first = await CreateStep(
            client,
            recipeId,
            "First step",
            rowVersion);

        var second = await CreateStep(
            client,
            recipeId,
            "Second step",
            first.RowVersion);

        Assert.Equal(2, second.Items.Count);
        Assert.Equal(1, second.Items[0].StepNumber);
        Assert.Equal(2, second.Items[1].StepNumber);
        Assert.Equal("First step", second.Items[0].Description);
        Assert.Equal("Second step", second.Items[1].Description);
    }

    [Fact]
    public async Task Create_step_without_authentication_returns_401()
    {
        var client = _factory.Client(null);
        var recipeId = await GetRecipeId();
        var rowVersion = await GetRecipeRowVersion(recipeId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps",
            new
            {
                description = "Unauthorized step",
                durationMinutes = 1,
                imageUrl = (string?)null,
                rowVersion
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_step_as_other_user_returns_403()
    {
        var client = _factory.Client(userId: "another-user");
        var recipeId = await GetRecipeId();
        var rowVersion = await GetRecipeRowVersion(recipeId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps",
            new
            {
                description = "Forbidden step",
                durationMinutes = 1,
                imageUrl = (string?)null,
                rowVersion
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_step_with_stale_row_version_returns_422()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();

        var originalRowVersion = await GetRecipeRowVersion(recipeId);

        var first = await CreateStep(
            client,
            recipeId,
            "First mutation",
            originalRowVersion);

        var second = await client.PostAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps",
            new
            {
                description = "Stale mutation",
                durationMinutes = 2,
                imageUrl = (string?)null,
                rowVersion = originalRowVersion
            });

        Assert.Equal((HttpStatusCode)422, second.StatusCode);
    }

    [Fact]
    public async Task Update_step_as_owner_updates_content_and_row_version()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();

        var rowVersion = await GetRecipeRowVersion(recipeId);

        var created = await CreateStep(
            client,
            recipeId,
            "Original description",
            rowVersion);

        var stepId = created.Items[0].Id;

        var response = await client.PutAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps/{stepId}",
            new
            {
                title = "Finish",
                description = "Updated description",
                durationMinutes = 10,
                imageUrl = "https://example.com/step.jpg",
                rowVersion = created.RowVersion
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<RecipeStepListResponse>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(stepId, result.Items[0].Id);
        Assert.Equal("Finish", result.Items[0].Title);
        Assert.Equal("Updated description", result.Items[0].Description);
        Assert.Equal(10, result.Items[0].TimerMinutes);
        Assert.Equal(
            "https://example.com/step.jpg",
            result.Items[0].ImageUrl);

        Assert.NotEqual(created.RowVersion, result.RowVersion);
    }

    [Fact]
    public async Task Update_step_from_other_recipe_returns_404()
    {
        var client = _factory.Client();

        var ownRecipeId = await GetRecipeId();
        var otherRecipeId = await CreateSecondRecipeAsync();

        var otherRowVersion =
            await GetRecipeRowVersion(otherRecipeId);

        var created = await CreateStep(
            client,
            otherRecipeId,
            "Other recipe step",
            otherRowVersion);

        var ownRowVersion =
            await GetRecipeRowVersion(ownRecipeId);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/recipes/{ownRecipeId}/steps/{created.Items[0].Id}",
            new
            {
                description = "Cross recipe mutation",
                durationMinutes = 10,
                imageUrl = (string?)null,
                rowVersion = ownRowVersion
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_step_renumbers_remaining_steps()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();

        var rowVersion = await GetRecipeRowVersion(recipeId);

        var first = await CreateStep(
            client,
            recipeId,
            "Step 1",
            rowVersion);

        var second = await CreateStep(
            client,
            recipeId,
            "Step 2",
            first.RowVersion);

        var third = await CreateStep(
            client,
            recipeId,
            "Step 3",
            second.RowVersion);

        Assert.Equal(3, third.Items.Count);

        var delete = await client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/v1/recipes/{recipeId}/steps/{second.Items[1].Id}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        rowVersion = third.RowVersion
                    })
            });

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await using var db = _factory.Database.NewContext();

        var remainingSteps = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                db.RecipeSteps
                    .Where(x => x.RecipeId == recipeId && !x.IsDeleted)
                    .OrderBy(x => x.StepNumber));

        Assert.Equal(2, remainingSteps.Count);
        Assert.Equal(1, remainingSteps[0].StepNumber);
        Assert.Equal(2, remainingSteps[1].StepNumber);
        Assert.Equal("Step 1", remainingSteps[0].Description);
        Assert.Equal("Step 3", remainingSteps[1].Description);
    }

    [Fact]
    public async Task Delete_step_with_stale_row_version_returns_422()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();

        var originalRowVersion =
            await GetRecipeRowVersion(recipeId);

        var created = await CreateStep(
            client,
            recipeId,
            "Step to delete",
            originalRowVersion);

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/recipes/{recipeId}/steps/{created.Items[0].Id}")
        {
            Content = JsonContent.Create(
                new
                {
                    rowVersion = originalRowVersion
                })
        };

        var response = await client.SendAsync(request);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Delete_step_from_other_recipe_returns_404()
    {
        var client = _factory.Client();

        var ownRecipeId = await GetRecipeId();
        var otherRecipeId = await CreateSecondRecipeAsync();

        var otherRowVersion =
            await GetRecipeRowVersion(otherRecipeId);

        var created = await CreateStep(
            client,
            otherRecipeId,
            "Other recipe step",
            otherRowVersion);

        var ownRowVersion =
            await GetRecipeRowVersion(ownRecipeId);

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/recipes/{ownRecipeId}/steps/{created.Items[0].Id}")
        {
            Content = JsonContent.Create(
                new
                {
                    rowVersion = ownRowVersion
                })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_steps_updates_full_order_and_preserves_unique_positive_numbers()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();
        var first = await CreateStep(client, recipeId, "Step 1", await GetRecipeRowVersion(recipeId));
        var second = await CreateStep(client, recipeId, "Step 2", first.RowVersion);
        var third = await CreateStep(client, recipeId, "Step 3", second.RowVersion);
        var requestedIds = new[]
        {
            third.Items[2].Id,
            third.Items[0].Id,
            third.Items[1].Id
        };

        var response = await ReorderSteps(client, recipeId, third.RowVersion, requestedIds);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<RecipeStepListResponse>())!;
        Assert.Equal(requestedIds, result.Items.Select(item => item.Id));
        Assert.Equal(new[] { 1, 2, 3 }, result.Items.Select(item => item.StepNumber));
        Assert.Equal(new[] { "Step 3", "Step 1", "Step 2" }, result.Items.Select(item => item.Description));
        Assert.NotEqual(third.RowVersion, result.RowVersion);

        await using var db = _factory.Database.NewContext();
        var storedSteps = await db.RecipeSteps
            .Where(step => step.RecipeId == recipeId && !step.IsDeleted)
            .OrderBy(step => step.StepNumber)
            .ToListAsync();
        Assert.Equal(requestedIds, storedSteps.Select(step => step.Id));
        Assert.All(storedSteps, step => Assert.True(step.StepNumber > 0));
        Assert.Equal(storedSteps.Count, storedSteps.Select(step => step.StepNumber).Distinct().Count());
        Assert.Equal(result.RowVersion, await GetRecipeRowVersion(recipeId));
    }

    [Fact]
    public async Task Reorder_empty_and_single_step_lists_are_valid_no_ops()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();
        var initialVersion = await GetRecipeRowVersion(recipeId);

        var empty = await ReorderSteps(client, recipeId, initialVersion, []);

        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        var emptyResult = (await empty.Content.ReadFromJsonAsync<RecipeStepListResponse>())!;
        Assert.Empty(emptyResult.Items);
        Assert.Equal(initialVersion, emptyResult.RowVersion);

        var created = await CreateStep(client, recipeId, "Only step", initialVersion);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await ReorderSteps(client, recipeId, initialVersion, [created.Items[0].Id])).StatusCode);
        var single = await ReorderSteps(
            client,
            recipeId,
            created.RowVersion,
            [created.Items[0].Id]);

        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        var singleResult = (await single.Content.ReadFromJsonAsync<RecipeStepListResponse>())!;
        Assert.Single(singleResult.Items);
        Assert.Equal(1, singleResult.Items[0].StepNumber);
        Assert.Equal(created.RowVersion, singleResult.RowVersion);
    }

    [Fact]
    public async Task Reorder_rejects_duplicate_incomplete_unknown_foreign_and_deleted_step_ids()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();
        var first = await CreateStep(client, recipeId, "Step 1", await GetRecipeRowVersion(recipeId));
        var second = await CreateStep(client, recipeId, "Step 2", first.RowVersion);
        var third = await CreateStep(client, recipeId, "Step 3", second.RowVersion);
        var ids = third.Items.Select(item => item.Id).ToArray();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await ReorderSteps(client, recipeId, third.RowVersion, [ids[0], ids[1], ids[1]])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ReorderSteps(client, recipeId, third.RowVersion, [ids[0], ids[1]])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ReorderSteps(client, recipeId, third.RowVersion, [ids[0], ids[1], Guid.NewGuid()])).StatusCode);

        var otherRecipeId = await CreateSecondRecipeAsync();
        var foreignStep = await CreateStep(
            client,
            otherRecipeId,
            "Foreign step",
            await GetRecipeRowVersion(otherRecipeId));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ReorderSteps(
                client,
                recipeId,
                third.RowVersion,
                [ids[0], ids[1], foreignStep.Items[0].Id])).StatusCode);

        await using (var db = _factory.Database.NewContext())
        {
            var deleted = await db.RecipeSteps.IgnoreQueryFilters()
                .SingleAsync(step => step.Id == ids[2]);
            deleted.IsDeleted = true;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ReorderSteps(client, recipeId, third.RowVersion, ids)).StatusCode);
    }

    [Fact]
    public async Task Two_reorders_using_the_same_row_version_allow_only_the_first()
    {
        var recipeId = await GetRecipeId();
        using var owner = _factory.Client();
        var created = await CreateStep(owner, recipeId, "Step 1", await GetRecipeRowVersion(recipeId));
        var second = await CreateStep(owner, recipeId, "Step 2", created.RowVersion);
        var ids = second.Items.Select(item => item.Id).Reverse().ToArray();

        using var otherUser = _factory.Client(userId: "another-user");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ReorderSteps(otherUser, recipeId, second.RowVersion, ids)).StatusCode);

        var success = await ReorderSteps(owner, recipeId, second.RowVersion, ids);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        var stale = await ReorderSteps(owner, recipeId, second.RowVersion, ids.Reverse().ToArray());
        Assert.Equal((HttpStatusCode)422, stale.StatusCode);
    }

    [Fact]
    public async Task Reorder_rolls_back_all_step_numbers_when_second_phase_fails()
    {
        var client = _factory.Client();
        var recipeId = await GetRecipeId();
        var first = await CreateStep(client, recipeId, "Step 1", await GetRecipeRowVersion(recipeId));
        var second = await CreateStep(client, recipeId, "Step 2", first.RowVersion);
        var third = await CreateStep(client, recipeId, "Step 3", second.RowVersion);

        await using (var db = _factory.Database.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER FailRecipeStepReorder
                BEFORE UPDATE OF "StepNumber" ON "RecipeSteps"
                WHEN NEW."StepNumber" = 1
                BEGIN
                    SELECT RAISE(ABORT, 'forced reorder failure');
                END;
                """);
        }

        var response = await ReorderSteps(
            client,
            recipeId,
            third.RowVersion,
            third.Items.Select(item => item.Id).Reverse().ToArray());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verify = _factory.Database.NewContext();
        var storedSteps = await verify.RecipeSteps
            .Where(step => step.RecipeId == recipeId && !step.IsDeleted)
            .OrderBy(step => step.StepNumber)
            .ToListAsync();
        Assert.Equal(new[] { "Step 1", "Step 2", "Step 3" }, storedSteps.Select(step => step.Description));
        Assert.Equal(new[] { 1, 2, 3 }, storedSteps.Select(step => step.StepNumber));
        Assert.Equal(third.RowVersion, await GetRecipeRowVersion(recipeId));
    }

    private async Task<RecipeStepListResponse> CreateStep(
        HttpClient client,
        Guid recipeId,
        string description,
        string rowVersion)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps",
            new
            {
                description,
                durationMinutes = 5,
                imageUrl = (string?)null,
                rowVersion
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<RecipeStepListResponse>();

        Assert.NotNull(result);

        return result!;
    }

    private static Task<HttpResponseMessage> ReorderSteps(
        HttpClient client,
        Guid recipeId,
        string rowVersion,
        IReadOnlyList<Guid> orderedStepIds)
        => client.PutAsJsonAsync(
            $"/api/v1/recipes/{recipeId}/steps/order",
            new { rowVersion, orderedStepIds });

    private async Task<Guid> GetRecipeId()
    {
        await using var db = _factory.Database.NewContext();

        return await db.Recipes
            .Select(x => x.Id)
            .FirstAsync();
    }

    private async Task<Guid> CreateSecondRecipeAsync()
    {
        var originalRecipeId = await GetRecipeId();

        await using var db = _factory.Database.NewContext();

        var original = await db.Recipes
            .FirstAsync(x => x.Id == originalRecipeId);

        var recipe = new Recipe
        {
            Title = "Second test recipe",
            Slug = $"second-test-{Guid.NewGuid():N}",
            Description = "Second recipe",
            Instructions = "Test instructions",
            PrepTime = 5,
            CookTime = 10,
            Servings = 2,
            Difficulty = original.Difficulty,
            Status = original.Status,
            CategoryId = original.CategoryId,
            AuthorId = original.AuthorId,
            Nutrition = new RecipeNutrition()
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        return recipe.Id;
    }

    private async Task<string> GetRecipeRowVersion(Guid recipeId)
    {
        await using var db = _factory.Database.NewContext();

        var recipe = await db.Recipes
            .FirstAsync(x => x.Id == recipeId);

        return Convert.ToBase64String(recipe.RowVersion);
    }

    private sealed record RecipeStepListResponse(
        IReadOnlyList<RecipeStepResponse> Items,
        string RowVersion);

    private sealed record RecipeStepResponse(
        Guid Id,
        int StepNumber,
        string? Title,
        string Description,
        int? TimerMinutes,
        string? ImageUrl,
        string RowVersion);
}
