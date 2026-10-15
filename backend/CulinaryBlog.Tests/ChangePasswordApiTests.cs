using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class ChangePasswordApiTests : IAsyncLifetime
{
    private const string Email = "password-change@test.local";
    private const string OldPassword = "Old-password@123";
    private const string NewPassword = "New-password@456";
    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await using var db = _factory.NewContext();
        await db.Database.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = Email, Email = Email, FullName = "Password User" };
        Assert.True((await users.CreateAsync(user, OldPassword)).Succeeded);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task AuthenticateAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = OldPassword });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private Task<HttpResponseMessage> ChangeAsync(string currentPassword, string newPassword, string confirmNewPassword) =>
        _client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword, newPassword, confirmNewPassword });

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(type, problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Change_password_requires_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ChangeAsync(OldPassword, NewPassword, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Change_password_replaces_login_password()
    {
        await AuthenticateAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(OldPassword, NewPassword, NewPassword)).StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = OldPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = NewPassword })).StatusCode);
    }

    [Fact]
    public async Task Change_password_rejects_an_incorrect_current_password()
    {
        await AuthenticateAsync();
        await AssertProblem(await ChangeAsync("wrong-password", NewPassword, NewPassword), HttpStatusCode.BadRequest,
            "INVALID_CURRENT_PASSWORD");
    }

    [Fact]
    public async Task Change_password_locks_after_five_incorrect_current_passwords()
    {
        await AuthenticateAsync();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await ChangeAsync("wrong-password", NewPassword, NewPassword);
            Assert.Equal(attempt == 5 ? HttpStatusCode.Locked : HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Change_password_rejects_a_mismatched_confirmation()
    {
        await AuthenticateAsync();
        await AssertProblem(await ChangeAsync(OldPassword, NewPassword, "other-password"), HttpStatusCode.BadRequest,
            "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Change_password_rejects_a_password_that_violates_identity_policy()
    {
        await AuthenticateAsync();
        await AssertProblem(await ChangeAsync(OldPassword, "weak", "weak"), HttpStatusCode.BadRequest,
            "VALIDATION_ERROR");
    }
}
