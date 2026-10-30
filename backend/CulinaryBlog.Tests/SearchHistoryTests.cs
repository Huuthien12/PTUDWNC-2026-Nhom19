using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class SearchHistoryTests : IAsyncLifetime
{
    private readonly RecipeApiFactory _factory = new();
    public Task InitializeAsync() => _factory.InitializeAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Requires_authentication()
    {
        using var client = _factory.Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/search-history")).StatusCode);
    }

    [Fact]
    public async Task Post_normalizes_and_refreshes_duplicate()
    {
        using var client = _factory.Client();
        var first = await client.PostAsJsonAsync("/api/v1/search-history", new { query = "  Pho Ga  " });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await client.PostAsJsonAsync("/api/v1/search-history", new { query = "pho ga" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await using var db = _factory.Database.NewContext();
        var rows = await db.SearchHistories.Where(x => x.UserId == _factory.AuthorId).ToListAsync();
        Assert.Single(rows); Assert.Equal("pho ga", rows[0].Query);
    }

    [Fact]
    public async Task Get_and_clear_are_isolated_to_current_user()
    {
        await using var db = _factory.Database.NewContext();
        db.Users.Add(new ApplicationUser { Id = "other", UserName = "other" });
        await db.SaveChangesAsync();
        db.SearchHistories.AddRange(new SearchHistory { UserId = _factory.AuthorId, Query = "mine", SearchedAt = DateTime.UtcNow.AddMinutes(-1) }, new SearchHistory { UserId = "other", Query = "other", SearchedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        using var client = _factory.Client();
        var list = await client.GetFromJsonAsync<List<Item>>("/api/v1/search-history");
        var entries = Assert.IsType<List<Item>>(list); Assert.Single(entries); Assert.Equal("mine", entries[0].Query);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/search-history")).StatusCode);
        await using var verify = _factory.Database.NewContext();
        Assert.DoesNotContain(await verify.SearchHistories.ToListAsync(), x => x.UserId == _factory.AuthorId);
        Assert.Contains(await verify.SearchHistories.ToListAsync(), x => x.UserId == "other");
    }

    [Fact]
    public async Task Delete_other_users_entry_returns_not_found_and_invalid_query_is_problem_details()
    {
        await using var db = _factory.Database.NewContext();
        db.Users.Add(new ApplicationUser { Id = "other", UserName = "other" });
        await db.SaveChangesAsync();
        var other = new SearchHistory { UserId = "other", Query = "other" }; db.SearchHistories.Add(other); await db.SaveChangesAsync();
        using var client = _factory.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/search-history/{other.Id}")).StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/v1/search-history", new { query = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Retains_only_twenty_entries_in_descending_order()
    {
        using var client = _factory.Client();
        for (var i = 0; i < 21; i++) Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/search-history", new { query = $"query {i}" })).StatusCode);
        var list = await client.GetFromJsonAsync<List<Item>>("/api/v1/search-history");
        Assert.Equal(20, list!.Count); Assert.Equal("query 20", list[0].Query);
    }

    private sealed record Item(Guid Id, string Query, DateTime SearchedAt);
}
