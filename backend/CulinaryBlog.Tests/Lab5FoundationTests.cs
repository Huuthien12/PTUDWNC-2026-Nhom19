using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class Lab5FoundationTests
{
    [Fact]
    public async Task Local_login_locks_after_five_failures_and_recovers_after_lockout()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser
            {
                UserName = "lockout@test.local", Email = "lockout@test.local", FullName = "Lockout Test"
            }, "Test-only@123456")).Succeeded);
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "lockout@test.local", password = "wrong" });
            Assert.Equal(attempt == 5 ? HttpStatusCode.Locked : HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using (var blocked = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "lockout@test.local", password = "Test-only@123456" }))
        {
            Assert.Equal(HttpStatusCode.Locked, blocked.StatusCode);
            using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
            Assert.Equal("AUTH_ACCOUNT_LOCKED", problem.RootElement.GetProperty("type").GetString());
            Assert.InRange(DateTimeOffset.Parse(problem.RootElement.GetProperty("lockedUntil").GetString()!) - DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync("lockout@test.local"))!;
            Assert.InRange(user.LockoutEnd!.Value - DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        }
        using var recovered = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "lockout@test.local", password = "Test-only@123456" });
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task Rate_limits_auth_api_upload_buckets_and_problem_response()
    {
        await using var factory = new AuthApiFactory { EnableRateLimiting = true };
        using var client = factory.CreateClient();
        await AssertLimit(client, "/api/v1/auth/unknown", HttpMethod.Get, 10);
        await AssertLimit(client, "/api/v1/unknown", HttpMethod.Get, 100);
        await AssertLimit(client, $"/api/v1/recipes/{Guid.NewGuid()}/images", HttpMethod.Post, 5);
    }

    [Fact]
    public async Task Openapi_v1_and_scalar_are_available_with_bearer_scheme()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("v1", document.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _));
        Assert.Equal("http", document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("type").GetString());
        var profileGet = document.RootElement.GetProperty("paths").GetProperty("/api/v1/auth/me").GetProperty("get");
        Assert.True(profileGet.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        using var scalar = await client.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }

    [Fact]
    public async Task Cors_allows_local_frontend_and_rejects_other_origins()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        allowed.Headers.Add("Origin", "http://localhost:3000");
        allowed.Headers.Add("Access-Control-Request-Method", "POST");
        using var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal("http://localhost:3000", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        denied.Headers.Add("Origin", "https://untrusted.example");
        denied.Headers.Add("Access-Control-Request-Method", "POST");
        using var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task AssertLimit(HttpClient client, string path, HttpMethod method, int limit)
    {
        for (var i = 0; i < limit; i++)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        using var rejectedRequest = new HttpRequestMessage(method, path);
        using var rejected = await client.SendAsync(rejectedRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("RATE_LIMIT_EXCEEDED", problem.RootElement.GetProperty("type").GetString());
    }
}
