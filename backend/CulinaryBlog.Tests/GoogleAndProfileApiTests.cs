using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.Authentication.Commands;
using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    public async Task Google_code_login_requires_and_forwards_the_pkce_verifier()
    {
        await using var factory = new AuthApiFactory();
        await using (var db = factory.NewContext()) await db.Database.EnsureCreatedAsync();
        factory.GoogleVerifier.Identity = new GoogleIdentity("google-code", "code@test.local", "Code User", null);

        var client = factory.CreateClient();
        var missing = await client.PostAsJsonAsync("/api/v1/auth/google", new { authorizationCode = "code" });
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { authorizationCode = "code", codeVerifier = "pkce-verifier" });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("code", factory.GoogleVerifier.AuthorizationCode);
        Assert.Equal("pkce-verifier", factory.GoogleVerifier.CodeVerifier);
    }

    [Fact]
    public async Task Google_code_exchange_forwards_the_pkce_verifier_to_google()
    {
        var handler = new RecordingHandler();
        var verifier = new GoogleCredentialVerifier(
            Options.Create(new GoogleAuthOptions { ClientId = "client", ClientSecret = "secret", RedirectUri = "https://app.test/login" }),
            new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://oauth2.googleapis.com/") }));

        await verifier.VerifyAsync(null, "authorization-code", "pkce-verifier");

        Assert.Contains("code=authorization-code", handler.Body);
        Assert.Contains("code_verifier=pkce-verifier", handler.Body);
        Assert.Contains("redirect_uri=https%3A%2F%2Fapp.test%2Flogin", handler.Body);
    }

    [Fact]
    public async Task Google_code_exchange_reports_safe_google_error_details()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest,
            JsonContent.Create(new { error = "invalid_grant", error_description = "Authorization code expired." }));
        var verifier = new GoogleCredentialVerifier(
            Options.Create(new GoogleAuthOptions { ClientId = "client", ClientSecret = "secret", RedirectUri = "https://app.test/login" }),
            new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://oauth2.googleapis.com/") }));

        var exception = await Assert.ThrowsAsync<ExternalAuthenticationException>(() =>
            verifier.VerifyAsync(null, "authorization-code", "pkce-verifier"));

        Assert.Equal("GOOGLE_TOKEN_EXCHANGE_FAILED", exception.ErrorCode);
        Assert.Contains("HTTP 400", exception.Message);
        Assert.Contains("invalid_grant", exception.Message);
        Assert.Contains("Authorization code expired.", exception.Message);
        Assert.DoesNotContain("authorization-code", exception.Message);
        Assert.DoesNotContain("pkce-verifier", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
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

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK, HttpContent? responseContent = null) : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(status) { Content = responseContent ?? JsonContent.Create(new { id_token = "not-a-jwt" }) };
        }
    }
}
