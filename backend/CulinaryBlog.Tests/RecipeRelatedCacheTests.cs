using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeRelatedCacheTests
{
    private sealed class SaveProbe : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public int FailAt { get; set; }
        public int Saves { get; private set; }
        public DbContext? MutationContext { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                MutationContext = data.Context;
                if (++Saves == FailAt) throw new DbUpdateException("Isolated failure before commit");
            }
            return ValueTask.FromResult(result);
        }
    }

    private static async Task<Recipe> SeedAsync(RecipeApiFactory factory)
    {
        await factory.InitializeAsync();
        await using var db = factory.Database.NewContext();
        var recipe = await db.Recipes.SingleAsync();
        (await db.Users.SingleAsync()).Email = "author@cache-tests.local";
        recipe.Status = RecipeStatus.Published;
        db.RecipeIngredients.Add(new RecipeIngredient { RecipeId = recipe.Id, Name = "Salt", Quantity = 1, Unit = "g" });
        db.RecipeSteps.AddRange(new RecipeStep { RecipeId = recipe.Id, StepNumber = 1, Description = "Mix" },
            new RecipeStep { RecipeId = recipe.Id, StepNumber = 2, Description = "Cook" });
        db.RecipeImages.AddRange(new RecipeImage { RecipeId = recipe.Id, IsPrimary = true, OriginalUrl = "https://example.test/a.png" },
            new RecipeImage { RecipeId = recipe.Id, OrderIndex = 1, OriginalUrl = "https://example.test/b.png" });
        await db.SaveChangesAsync();
        return await db.Recipes.AsNoTracking().Include(r => r.Ingredients).Include(r => r.Steps).Include(r => r.Images).SingleAsync();
    }

    private static async Task<HttpResponseMessage> MutateAsync(HttpClient client, Recipe recipe, string action, string? version = null)
    {
        var rv = version ?? Convert.ToBase64String(recipe.RowVersion);
        var path = $"/api/v1/recipes/{recipe.Id}";
        var ingredient = new { name = "Flour", quantity = 2, unit = "g", sortOrder = 1, rowVersion = rv };
        var step = new { description = "New description", durationMinutes = 2, rowVersion = rv };
        var imageId = action == "image-primary" ? recipe.Images.Single(i => !i.IsPrimary).Id : recipe.Images.Single(i => i.IsPrimary).Id;
        return action switch
        {
            "ingredient-add" => await client.PostAsJsonAsync($"{path}/ingredients", ingredient),
            "ingredient-update" => await client.PutAsJsonAsync($"{path}/ingredients/{recipe.Ingredients.First().Id}", ingredient),
            "ingredient-delete" => await client.SendAsync(new(HttpMethod.Delete, $"{path}/ingredients/{recipe.Ingredients.First().Id}")
                { Content = JsonContent.Create(new { rowVersion = rv }) }),
            "step-add" => await client.PostAsJsonAsync($"{path}/steps", step),
            "step-update" => await client.PutAsJsonAsync($"{path}/steps/{recipe.Steps.First().Id}", step),
            "step-delete" => await client.SendAsync(new(HttpMethod.Delete, $"{path}/steps/{recipe.Steps.OrderBy(s => s.StepNumber).First().Id}")
                { Content = JsonContent.Create(new { rowVersion = rv }) }),
            "image-primary" => await client.PatchAsJsonAsync($"{path}/images/{imageId}/primary", new { rowVersion = rv }),
            "image-delete" => await client.SendAsync(new(HttpMethod.Delete, $"{path}/images/{imageId}")
                { Content = JsonContent.Create(new { rowVersion = rv }) }),
            "image-upload" => await client.PostAsync($"{path}/images", Upload(rv)),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static MultipartFormDataContent Upload(string version)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(Guid.NewGuid().ToString()), "uploadId");
        var file = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "photo.png");
        content.Add(new StringContent(version), "rowVersion");
        return content;
    }

    public static TheoryData<string> Actions => new()
    { "ingredient-add", "ingredient-update", "ingredient-delete", "step-add", "step-update", "step-delete",
      "image-primary", "image-delete", "image-upload" };

    [Theory, MemberData(nameof(Actions))]
    public async Task Related_mutation_evicts_detail_and_list_only_after_entire_commit(string action)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        var probe = new SaveProbe();
        factory.Database.SaveInterceptor = probe;
        var recipe = await SeedAsync(factory);
        probe.Armed = true;
        factory.Cache.OnRemoveAsync = async key =>
        {
            Assert.Equal($"recipe:{recipe.Slug}", key);
            Assert.NotNull(probe.MutationContext);
            Assert.Null(probe.MutationContext.Database.CurrentTransaction);
            await using var db = factory.Database.NewContext();
            Assert.NotEqual(recipe.RowVersion, (await db.Recipes.SingleAsync()).RowVersion);
        };
        using var client = factory.Client();
        var response = await MutateAsync(client, recipe, action);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { $"recipe:{recipe.Slug}" }, factory.Cache.RemovedKeys);
        Assert.Equal(new[] { "recipes" }, factory.OutputCache.EvictedTags);
        if (action == "step-delete")
        {
            await using var db = factory.Database.NewContext();
            Assert.Equal(1, (await db.RecipeSteps.SingleAsync()).StepNumber);
        }
    }

    [Theory, MemberData(nameof(Actions))]
    public async Task Stale_child_mutations_never_evict(string action)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        var recipe = await SeedAsync(factory);
        using var client = factory.Client();
        var response = await MutateAsync(client, recipe, action, Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(factory.Cache.RemovedKeys);
        Assert.Empty(factory.OutputCache.EvictedTags);
    }

    [Theory, MemberData(nameof(Actions))]
    public async Task Unauthorized_child_mutations_never_evict(string action)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        var recipe = await SeedAsync(factory);
        using var client = factory.Client(userId: "another-author");
        var response = await MutateAsync(client, recipe, action);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Cache.RemovedKeys);
        Assert.Empty(factory.OutputCache.EvictedTags);
    }

    [Theory, MemberData(nameof(Actions))]
    public async Task Cache_eviction_failure_after_child_commit_keeps_success_and_attempts_list(string action)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() { FailEvict = true } };
        var recipe = await SeedAsync(factory);
        factory.Cache.OnRemoveAsync = _ => throw new IOException("Transport failure");
        using var client = factory.Client();
        var response = await MutateAsync(client, recipe, action);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Contains($"recipe:{recipe.Slug}", factory.Cache.RemovedKeys);
        Assert.Contains("recipes", factory.OutputCache.EvictedTags);
        await using var db = factory.Database.NewContext();
        Assert.NotEqual(recipe.RowVersion, (await db.Recipes.SingleAsync()).RowVersion);
    }

    [Theory]
    [InlineData("step-delete")]
    [InlineData("image-primary")]
    [InlineData("image-delete")]
    public async Task Second_save_failure_rolls_back_entire_transaction_without_eviction(string action)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        var probe = new SaveProbe { FailAt = 2 };
        factory.Database.SaveInterceptor = probe;
        var recipe = await SeedAsync(factory);
        probe.Armed = true;
        using var client = factory.Client();
        var response = await MutateAsync(client, recipe, action);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, probe.Saves);
        Assert.Empty(factory.Cache.RemovedKeys);
        Assert.Empty(factory.OutputCache.EvictedTags);
        await using var db = factory.Database.NewContext();
        Assert.Equal(recipe.RowVersion, (await db.Recipes.SingleAsync()).RowVersion);
        Assert.Equal(2, await db.RecipeImages.CountAsync());
        Assert.Equal(2, await db.RecipeSteps.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Category_or_author_change_evicts_all_affected_details_and_attempts_list_despite_cache_failure(bool author)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() { FailEvict = true } };
        var recipe = await SeedAsync(factory);
        await using (var db = factory.Database.NewContext())
        {
            db.Recipes.Add(new Recipe { Title = "Second", Slug = "second", AuthorId = recipe.AuthorId, CategoryId = recipe.CategoryId });
            await db.SaveChangesAsync();
        }
        factory.Cache.OnRemoveAsync = _ => throw new IOException("Cache unavailable");
        using var client = factory.Client(author ? "Author" : "Admin");
        var response = author
            ? await client.PatchAsJsonAsync("/api/v1/auth/me", new { fullName = "New author", avatarUrl = "https://example.test/avatar.png" })
            : await client.PutAsJsonAsync($"/api/v1/categories/{recipe.CategoryId}", new { name = "New category", description = "Updated" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"recipe:{recipe.Slug}", factory.Cache.RemovedKeys);
        Assert.Contains("recipe:second", factory.Cache.RemovedKeys);
        Assert.Contains("recipes", factory.OutputCache.EvictedTags);
        await using var verify = factory.Database.NewContext();
        if (author) Assert.Equal("New author", (await verify.Users.SingleAsync()).FullName);
        else Assert.Equal("New category", (await verify.Categories.SingleAsync()).Name);
    }
}
