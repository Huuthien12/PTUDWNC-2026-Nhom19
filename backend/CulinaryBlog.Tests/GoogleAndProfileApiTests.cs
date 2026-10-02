using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.Authentication.Commands;
using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class GoogleAndProfileApiTests
{
    [Fact]
    public async Task Google_login_verifies_identity_creates_author_and_uses_existing_token_flow()
    {
        await using var factory = new AuthApiFactory();
        await using (var db = factory.NewContext()) await db.Database.EnsureCreatedAsync();
        factory.GoogleVerifier.Identity = new GoogleIdentity("google-subject", "google@test.local", "Google User", "https://example.test/avatar.png");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/google", new { idToken = "verified-by-fake" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        Assert.Equal("google@test.local", tokens.User.Email);
        Assert.Contains("Author", tokens.User.Roles);
        await using var verify = factory.NewContext();
        Assert.Equal(1, await verify.Users.CountAsync());
        Assert.Equal(new CulinaryBlog.Infrastructure.Authentication.RefreshTokenGenerator().Hash(tokens.RefreshToken),
            (await verify.RefreshTokens.SingleAsync()).Token);
    }

    [Fact]
    public async Task Google_login_rejects_unverified_credentials()
    {
        await using var factory = new AuthApiFactory();
        await using (var db = factory.NewContext()) await db.Database.EnsureCreatedAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/google", new { idToken = "invalid" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Google_login_links_a_verified_email_to_an_existing_account()
    {
        await using var factory = new AuthApiFactory();
        await using (var db = factory.NewContext()) await db.Database.EnsureCreatedAsync();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Existing User", email = "existing@test.local", userName = "ExistingUser", password = "Existing-only@123456"
        });
        factory.GoogleVerifier.Identity = new GoogleIdentity("existing-google", "existing@test.local", "Google Name", null);

        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "verified-by-fake" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verify = factory.NewContext();
        var user = await verify.Users.SingleAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var identityUser = (await users.FindByIdAsync(user.Id))!;
        Assert.Contains(await users.GetLoginsAsync(identityUser), login =>
            login.LoginProvider == "Google" && login.ProviderKey == "existing-google");
    }

    [Fact]
    public async Task Me_returns_and_updates_only_the_current_user_profile()
    {
        await using var factory = new AuthApiFactory();
        await using (var db = factory.NewContext()) await db.Database.EnsureCreatedAsync();
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Profile User", email = "profile@test.local", userName = "ProfileUser", password = "Profile-only@123456"
        });
        var tokens = (await register.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var get = await client.GetAsync("/api/v1/auth/me");
        var before = (await get.Content.ReadFromJsonAsync<UserProfileDto>())!;
        var patch = await client.PatchAsJsonAsync("/api/v1/auth/me", new { fullName = "Updated Profile", avatarUrl = "https://example.test/new.png", email = "ignored@test.local" });
        var after = (await patch.Content.ReadFromJsonAsync<UserProfileDto>())!;

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal("Updated Profile", after.FullName);
        Assert.Equal("https://example.test/new.png", after.AvatarUrl);
    }

    [Fact]
    public async Task Me_requires_authentication_and_rejects_invalid_profile_input()
    {
        await using var factory = new AuthApiFactory();
        await using (var db = factory.NewContext()) await db.Database.EnsureCreatedAsync();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Valid User", email = "valid@test.local", userName = "ValidUser", password = "Profile-only@123456"
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await register.Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken);

        var patch = await client.PatchAsJsonAsync("/api/v1/auth/me", new { fullName = " ", avatarUrl = "not-a-url" });

        Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
        Assert.Equal("application/problem+json", patch.Content.Headers.ContentType?.MediaType);
    }
}
