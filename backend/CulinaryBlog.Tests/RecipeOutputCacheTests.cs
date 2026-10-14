using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeOutputCacheTests
{
    [Fact]
    public async Task Public_list_hits_varies_by_all_filters_and_expires_after_two_minutes()
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        await factory.InitializeAsync();
        using var client = factory.Client(null);
        const string path = "/api/v1/recipes?page=1&pageSize=12";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(1, factory.OutputCache.Hits);
        foreach (var query in new[] { "page=2", "pageSize=5", $"categoryId={factory.CategoryId}", "difficulty=1",
                     "maxCookTime=10", "minServings=2", "sortBy=title", "sortOrder=asc" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/recipes?{query}")).StatusCode);
        Assert.Equal(1, factory.OutputCache.Hits);
        Assert.Equal(9, factory.OutputCache.Lifetimes.Count);
        Assert.All(factory.OutputCache.Lifetimes, ttl => Assert.Equal(TimeSpan.FromMinutes(2), ttl));
        factory.OutputCache.Now += TimeSpan.FromMinutes(2);
        await client.GetAsync(path);
        Assert.Equal(10, factory.OutputCache.Lifetimes.Count);
    }

    [Fact]
    public async Task Authenticated_and_cookie_requests_bypass_public_entries_and_errors_are_not_cached()
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        await factory.InitializeAsync();
        using var anonymous = factory.Client(null);
        await anonymous.GetAsync("/api/v1/recipes");
        using var owner = factory.Client();
        var response = await owner.GetAsync("/api/v1/recipes");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>();
        Assert.Single(page!.Items); // The anonymous response was empty: draft is owner-only.
        using var other = factory.Client(userId: "other-author");
        var otherPage = await (await other.GetAsync("/api/v1/recipes")).Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>();
        Assert.Empty(otherPage!.Items);
        anonymous.DefaultRequestHeaders.Add("Cookie", "session=ignored-test-cookie");
        await anonymous.GetAsync("/api/v1/recipes");
        Assert.Equal(0, factory.OutputCache.Hits);
        Assert.Single(factory.OutputCache.Lifetimes);
        anonymous.DefaultRequestHeaders.Remove("Cookie");
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/v1/recipes?page=0")).StatusCode);
        Assert.Single(factory.OutputCache.Lifetimes);
    }

    [Fact]
    public async Task Lifecycle_commit_evicts_list_and_new_public_response_includes_published_recipe()
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() };
        await factory.InitializeAsync();
        using var anonymous = factory.Client(null);
        await anonymous.GetAsync("/api/v1/recipes");
        await using var db = factory.Database.NewContext();
        var recipe = await db.Recipes.SingleAsync();
        // Publication rules require enough content; seed solely in this isolated fixture.
        recipe.Instructions = "Cook thoroughly";
        recipe.PrepTime = 5; recipe.CookTime = 10; recipe.Servings = 2;
        db.RecipeIngredients.Add(new RecipeIngredient { RecipeId = recipe.Id, Name = "Salt", Quantity = 1, Unit = "g" });
        db.RecipeSteps.Add(new RecipeStep { RecipeId = recipe.Id, StepNumber = 1, Description = "Mix" });
        await db.SaveChangesAsync();
        using var owner = factory.Client();
        var response = await owner.PatchAsJsonAsync($"/api/v1/recipes/{recipe.Id}/publish",
            new { rowVersion = Convert.ToBase64String(recipe.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("recipes", factory.OutputCache.EvictedTags);
        var page = await (await anonymous.GetAsync("/api/v1/recipes")).Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>();
        Assert.Single(page!.Items);
        Assert.Equal(2, factory.OutputCache.Lifetimes.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Output_storage_failure_does_not_fail_valid_database_read(bool read)
    {
        await using var factory = new RecipeApiFactory { OutputCache = new() { FailRead = read, FailWrite = !read } };
        await factory.InitializeAsync();
        using var anonymous = factory.Client(null);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/v1/recipes")).StatusCode);
    }
}
