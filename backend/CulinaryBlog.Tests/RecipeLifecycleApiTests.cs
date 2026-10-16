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
    private static async Task<Recipe> SeedAsync(RecipeApiFactory factory, RecipeStatus status = RecipeStatus.Draft,
        bool complete = true, string? title = null)
    {
        await factory.InitializeAsync();
        await using var db = factory.Database.NewContext();
        var recipe = await db.Recipes.SingleAsync();
        if (title is not null) recipe.Title = title;
        recipe.Status = status;
        recipe.PublishedAt = status == RecipeStatus.Published ? DateTime.UtcNow.AddDays(-2) : null;
        recipe.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        if (complete)
        {
            recipe.Ingredients.Add(new RecipeIngredient { Name = "Salt", Quantity = 1, Unit = "g", OrderIndex = 1 });
            recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });
        }
        recipe.Images.Add(new RecipeImage { OriginalUrl = "https://example.test/image.jpg", OrderIndex = 1, IsPrimary = true });
        db.RecipeIngredients.AddRange(recipe.Ingredients);
        db.RecipeSteps.AddRange(recipe.Steps);
        db.RecipeImages.AddRange(recipe.Images);
        await db.SaveChangesAsync();
        return recipe;
    }

    private static async Task<IReadOnlyList<string>> Suggestions(HttpClient client, string query) =>
        (await (await client.GetAsync($"/api/v1/recipes/suggestions?q={query}"))
            .Content.ReadFromJsonAsync<RecipeSuggestionsDto>())!.Items.Select(item => item.Text).ToList();

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
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Theory]
    [InlineData("publish", RecipeStatus.Draft, RecipeStatus.Published)]
    [InlineData("unpublish", RecipeStatus.Published, RecipeStatus.Draft)]
    [InlineData("archive", RecipeStatus.Draft, RecipeStatus.Archived)]
    [InlineData("archive", RecipeStatus.Published, RecipeStatus.Archived)]
    [InlineData("publish", RecipeStatus.Published, RecipeStatus.Published)]
    [InlineData("unpublish", RecipeStatus.Draft, RecipeStatus.Draft)]
    public async Task Owner_can_apply_each_mapped_lifecycle_action(string route, RecipeStatus beforeStatus, RecipeStatus expectedStatus)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, beforeStatus);
        factory.Cache.OnRemoveAsync = async _ =>
        {
            await using var committedDb = factory.Database.NewContext();
            var committed = await committedDb.Recipes.SingleAsync(x => x.Id == recipe.Id);
            Assert.Equal(expectedStatus, committed.Status);
            Assert.NotEqual(recipe.RowVersion, committed.RowVersion);
            Assert.Equal(recipe.PublishedAt, committed.PublishedAt);
        };
        using var client = factory.Client();

        var response = await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/{route}", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
        Assert.Equal(expectedStatus, dto.Status);
        Assert.NotEqual(Convert.ToBase64String(recipe.RowVersion), dto.RowVersion);
        var expectedKeys = (beforeStatus == RecipeStatus.Published) != (expectedStatus == RecipeStatus.Published)
            ? new[] { $"recipe:{recipe.Slug}", "categories:all" }
            : new[] { $"recipe:{recipe.Slug}" };
        Assert.Equal(expectedKeys, factory.Cache.RemovedKeys);
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(expectedStatus, saved.Status);
        Assert.Equal(dto.RowVersion, Convert.ToBase64String(saved.RowVersion));
        Assert.Equal(recipe.PublishedAt, saved.PublishedAt);
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
        Assert.Empty(factory.Cache.RemovedKeys);
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
        Assert.NotEqual(archived.RowVersion, repeated.RowVersion);
        Assert.Equal(new[] { $"recipe:{recipe.Slug}", $"recipe:{recipe.Slug}" }, factory.Cache.RemovedKeys);
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
        Assert.Equal(new[] { $"recipe:{recipe.Slug}", "categories:all" }, factory.Cache.RemovedKeys);
    }

    [Fact]
    public async Task Warm_suggestions_show_recipe_immediately_after_publish()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, title: "Lifecycle Pho");
        using var publicClient = factory.Client(role: null);
        using var owner = factory.Client();
        Assert.DoesNotContain(recipe.Title, await Suggestions(publicClient, "lifecycle"));

        var response = await owner.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(recipe.Title, await Suggestions(publicClient, "lifecycle"));
    }

    [Theory]
    [InlineData("unpublish")]
    [InlineData("archive")]
    public async Task Warm_suggestions_hide_recipe_immediately_after_public_lifecycle_exit(string action)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, RecipeStatus.Published, title: "Lifecycle Pho");
        using var publicClient = factory.Client(role: null);
        using var owner = factory.Client();
        Assert.Contains(recipe.Title, await Suggestions(publicClient, "lifecycle"));

        var response = await owner.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/{action}", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(recipe.Title, await Suggestions(publicClient, "lifecycle"));
    }

    [Fact]
    public async Task Warm_suggestions_hide_recipe_immediately_after_soft_delete()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, RecipeStatus.Published, title: "Lifecycle Pho");
        using var publicClient = factory.Client(role: null);
        using var owner = factory.Client();
        Assert.Contains(recipe.Title, await Suggestions(publicClient, "lifecycle"));

        var response = await owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/recipes/{recipe.Id}") { Content = JsonContent.Create(Body(recipe)) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(recipe.Title, await Suggestions(publicClient, "lifecycle"));
        await using var db = factory.Database.NewContext();
        Assert.True((await db.Recipes.IgnoreQueryFilters().SingleAsync(item => item.Id == recipe.Id)).IsDeleted);
    }

    [Fact]
    public async Task Incomplete_publish_is_rfc7807_400()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, complete: false);
        using var owner = factory.Client();

        await AssertProblem(await owner.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe)),
            400, "RECIPE_PUBLISH_INCOMPLETE");
    }

    [Fact]
    public async Task Suggestion_invalidation_failure_after_publish_does_not_undo_commit()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        factory.Cache.OnSetAsync = key => key == "search:suggest:version"
            ? throw new IOException("Redis unavailable") : Task.CompletedTask;
        using var owner = factory.Client();

        var response = await owner.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = factory.Database.NewContext();
        Assert.Equal(RecipeStatus.Published, (await db.Recipes.SingleAsync(item => item.Id == recipe.Id)).Status);
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
        Assert.Empty(factory.Cache.RemovedKeys);
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
        Assert.Equal(new[] { $"recipe:{recipe.Slug}", "categories:all" }, factory.Cache.RemovedKeys);
        factory.Cache.RemovedKeys.Clear();
        await AssertProblem(await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/unpublish", Body(recipe)), 422, "RECIPE_CONCURRENCY_CONFLICT");

        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Published, saved.Status);
        Assert.Equal(current.RowVersion, Convert.ToBase64String(saved.RowVersion));
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Theory]
    [InlineData("publish", "RECIPE_PUBLISH_INVALID_STATE")]
    [InlineData("unpublish", "RECIPE_UNPUBLISH_INVALID_STATE")]
    public async Task Invalid_lifecycle_transition_does_not_evict_cache(string route, string errorCode)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, RecipeStatus.Archived);
        using var client = factory.Client();

        await AssertProblem(await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/{route}", Body(recipe)), 400, errorCode);

        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Fact]
    public async Task Failed_lifecycle_save_does_not_evict_cache()
    {
        await using var factory = new RecipeApiFactory { FailWithSlugConstraint = true };
        var recipe = await SeedAsync(factory);
        using var client = factory.Client();

        var response = await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish", Body(recipe));

        await AssertProblem(response, 409, "RECIPE_SLUG_EXISTS");
        Assert.Empty(factory.Cache.RemovedKeys);
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Draft, saved.Status);
        Assert.Equal(recipe.RowVersion, saved.RowVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cache_failure_after_lifecycle_commit_does_not_undo_saved_state(bool failCategoryRemoval)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, RecipeStatus.Published);
        var observedCommittedState = false;
        factory.Cache.OnRemoveAsync = async key =>
        {
            await using var db = factory.Database.NewContext();
            var saved = await db.Recipes.SingleAsync(x => x.Id == recipe.Id);
            Assert.Equal(RecipeStatus.Draft, saved.Status);
            Assert.NotEqual(recipe.RowVersion, saved.RowVersion);
            observedCommittedState = true;
            if (key == (failCategoryRemoval ? "categories:all" : $"recipe:{recipe.Slug}"))
                throw new IOException("Test cache removal failure");
        };
        using var client = factory.Client();

        var response = await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/unpublish", Body(recipe));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(observedCommittedState);
        Assert.Equal(new[] { $"recipe:{recipe.Slug}", "categories:all" }, factory.Cache.RemovedKeys);
        await using var verification = factory.Database.NewContext();
        var persisted = await verification.Recipes.SingleAsync(x => x.Id == recipe.Id);
        Assert.Equal(RecipeStatus.Draft, persisted.Status);
        Assert.Equal(recipe.PublishedAt, persisted.PublishedAt);
    }

    [Theory]
    [InlineData(RecipeStatus.Published)]
    [InlineData(RecipeStatus.Draft)]
    [InlineData(RecipeStatus.Archived)]
    public async Task Owner_soft_delete_keeps_recipe_and_children_but_hides_all_recipe_reads(RecipeStatus status)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, status);
        factory.Cache.OnRemoveAsync = async _ =>
        {
            await using var committedDb = factory.Database.NewContext();
            var committed = await committedDb.Recipes.IgnoreQueryFilters().SingleAsync(x => x.Id == recipe.Id);
            Assert.True(committed.IsDeleted);
            Assert.Equal(status, committed.Status);
            Assert.NotEqual(recipe.RowVersion, committed.RowVersion);
            Assert.Null(committedDb.Database.CurrentTransaction);
        };
        using var client = factory.Client();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}")
        {
            Content = JsonContent.Create(Body(recipe))
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var expectedKeys = status == RecipeStatus.Published
            ? new[] { $"recipe:{recipe.Slug}", "categories:all" }
            : new[] { $"recipe:{recipe.Slug}" };
        Assert.Equal(expectedKeys, factory.Cache.RemovedKeys);
        await AssertProblem(await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}")
        {
            Content = JsonContent.Create(Body(recipe))
        }), 404, "RECIPE_NOT_FOUND");
        Assert.Equal(expectedKeys, factory.Cache.RemovedKeys);
        await using var db = factory.Database.NewContext();
        var deleted = await db.Recipes.IgnoreQueryFilters().SingleAsync(x => x.Id == recipe.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal(1, await db.RecipeIngredients.IgnoreQueryFilters().CountAsync(x => x.RecipeId == recipe.Id));
        Assert.Equal(1, await db.RecipeSteps.IgnoreQueryFilters().CountAsync(x => x.RecipeId == recipe.Id));
        Assert.Equal(1, await db.RecipeImages.IgnoreQueryFilters().CountAsync(x => x.RecipeId == recipe.Id));
        Assert.Null(await db.Recipes.SingleOrDefaultAsync(x => x.Id == recipe.Id));
        await AssertProblem(await client.GetAsync($"/api/v1/recipes/{recipe.Slug}"), 404, "RECIPE_NOT_FOUND");
        Assert.DoesNotContain(recipe.Id.ToString(), await (await client.GetAsync("/api/v1/recipes")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_soft_delete_another_author_recipe()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var client = factory.Client("Admin", userId: "admin-user");

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}") { Content = JsonContent.Create(Body(recipe)) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_requires_owner_and_existing_recipe()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var guest = factory.Client(role: null);
        using var otherAuthor = factory.Client(userId: "other-author");
        using var owner = factory.Client();

        await AssertProblem(await guest.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}") { Content = JsonContent.Create(Body(recipe)) }), 401, "AUTH_TOKEN_INVALID");
        await AssertProblem(await otherAuthor.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}") { Content = JsonContent.Create(Body(recipe)) }), 403, "RECIPE_FORBIDDEN");
        await AssertProblem(await owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{Guid.NewGuid()}") { Content = JsonContent.Create(Body(recipe)) }), 404, "RECIPE_NOT_FOUND");
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Fact]
    public async Task Stale_delete_row_version_returns_rfc7807_422()
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory);
        using var client = factory.Client();
        var winner = await client.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/archive", Body(recipe));
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);

        factory.Cache.RemovedKeys.Clear();

        await AssertProblem(await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}") { Content = JsonContent.Create(Body(recipe)) }), 422, "RECIPE_CONCURRENCY_CONFLICT");
        Assert.Empty(factory.Cache.RemovedKeys);
        await using var db = factory.Database.NewContext();
        Assert.False((await db.Recipes.SingleAsync(x => x.Id == recipe.Id)).IsDeleted);
    }

    [Fact]
    public async Task Failed_delete_save_does_not_evict_cache()
    {
        await using var factory = new RecipeApiFactory { FailWithSlugConstraint = true };
        var recipe = await SeedAsync(factory, RecipeStatus.Published);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}")
        {
            Content = JsonContent.Create(Body(recipe))
        }), 409, "RECIPE_SLUG_EXISTS");

        Assert.Empty(factory.Cache.RemovedKeys);
        await using var db = factory.Database.NewContext();
        var saved = await db.Recipes.SingleAsync(x => x.Id == recipe.Id);
        Assert.False(saved.IsDeleted);
        Assert.Equal(recipe.RowVersion, saved.RowVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cache_failure_after_delete_commit_does_not_undo_soft_delete(bool failCategoryRemoval)
    {
        await using var factory = new RecipeApiFactory();
        var recipe = await SeedAsync(factory, RecipeStatus.Published);
        factory.Cache.OnRemoveAsync = async key =>
        {
            await using var db = factory.Database.NewContext();
            var saved = await db.Recipes.IgnoreQueryFilters().SingleAsync(x => x.Id == recipe.Id);
            Assert.True(saved.IsDeleted);
            Assert.NotEqual(recipe.RowVersion, saved.RowVersion);
            if (key == (failCategoryRemoval ? "categories:all" : $"recipe:{recipe.Slug}"))
                throw new IOException("Test cache removal failure");
        };
        using var client = factory.Client();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}")
        {
            Content = JsonContent.Create(Body(recipe))
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new[] { $"recipe:{recipe.Slug}", "categories:all" }, factory.Cache.RemovedKeys);
        await using var verification = factory.Database.NewContext();
        Assert.True((await verification.Recipes.IgnoreQueryFilters().SingleAsync(x => x.Id == recipe.Id)).IsDeleted);
    }
}
