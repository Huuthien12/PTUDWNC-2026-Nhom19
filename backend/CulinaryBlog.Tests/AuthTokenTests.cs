using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Authentication;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class AuthTokenTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;
    private const string Email = "author@test.local";
    private const string Password = "Test-only@123456";
    private string _userId = "";

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = Email, Email = Email, FullName = "Test Author" };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        _userId = user.Id;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<AuthResponseDto> Login()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private Task<HttpResponseMessage> Refresh(string? token) =>
        _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token });

    private async Task<HttpResponseMessage> Logout(string? access, string? refresh)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = refresh })
        };
        if (access is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return await _client.SendAsync(request);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Login_and_refresh_return_full_srs_response_with_current_profile_and_roles()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = (await users.FindByIdAsync(_userId))!;
        Assert.True((await users.SetUserNameAsync(user, "test.author")).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole("Author"))).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Author")).Succeeded);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        var first = await AssertAuthResponse(login, "Test Author", "test.author", null, ["Author"]);

        // Refresh must use today's profile/roles, not data cached in the previous JWT.
        user.FullName = "Updated Author";
        user.AvatarUrl = "https://example.test/avatar.png";
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.True((await users.RemoveFromRoleAsync(user, "Author")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);

        var second = await AssertAuthResponse(await Refresh(first.RefreshToken),
            "Updated Author", "test.author", "https://example.test/avatar.png", ["Admin"]);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
    }

    [Fact]
    public async Task Response_includes_empty_roles_array_and_null_avatar_when_unset()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        var first = await AssertAuthResponse(login, "Test Author", Email, null, []);
        await AssertAuthResponse(await Refresh(first.RefreshToken), "Test Author", Email, null, []);
    }

    private async Task<AuthResponseDto> AssertAuthResponse(HttpResponseMessage response,
        string fullName, string userName, string? avatarUrl, string[] expectedRoles)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "accessToken", "expiresAt", "refreshToken", "tokenType", "user" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal("Bearer", json.GetProperty("tokenType").GetString());
        Assert.Equal(JsonValueKind.String, json.GetProperty("accessToken").ValueKind);
        Assert.Equal(JsonValueKind.String, json.GetProperty("refreshToken").ValueKind);
        var expiresAt = json.GetProperty("expiresAt").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, expiresAt.Kind);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("accessToken").GetString());
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(jwt.Payload.Expiration!.Value).UtcDateTime, expiresAt);

        var profile = json.GetProperty("user");
        // Exact property set also guards against leaking Identity password/security fields.
        Assert.Equal(new[] { "avatarUrl", "email", "fullName", "id", "roles", "userName" },
            profile.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(_userId, profile.GetProperty("id").GetString());
        Assert.Equal(fullName, profile.GetProperty("fullName").GetString());
        Assert.Equal(Email, profile.GetProperty("email").GetString());
        Assert.Equal(userName, profile.GetProperty("userName").GetString());
        Assert.Equal(avatarUrl is null ? JsonValueKind.Null : JsonValueKind.String, profile.GetProperty("avatarUrl").ValueKind);
        Assert.Equal(avatarUrl, profile.GetProperty("avatarUrl").GetString());
        Assert.Equal(JsonValueKind.Array, profile.GetProperty("roles").ValueKind);
        Assert.Equal(expectedRoles.OrderBy(r => r), profile.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).OrderBy(r => r));
        Assert.Equal(expectedRoles.OrderBy(r => r), jwt.Claims
            .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).Select(c => c.Value).OrderBy(r => r));
        return json.Deserialize<AuthResponseDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public async Task Login_stores_only_hash_and_rotation_rejects_old_token_but_preserves_new_token()
    {
        var first = await Login();
        Assert.Equal("Bearer", first.TokenType);
        Assert.Equal(16, Convert.FromBase64String(first.RefreshToken).Length);
        await using var db = _factory.NewContext();
        var stored = await db.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.Equal(new RefreshTokenGenerator().Hash(first.RefreshToken), stored.Token);
        Assert.Equal(_factory.Clock.Now.UtcDateTime.AddDays(7), stored.ExpiresAt);

        var response = await Refresh(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        stored = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == stored.Id);
        Assert.True(stored.IsRevoked);
        Assert.Equal(_factory.Clock.Now.UtcDateTime, stored.RevokedAt);
        Assert.Equal(new RefreshTokenGenerator().Hash(second.RefreshToken), stored.ReplacedByToken);

        await AssertProblem(await Refresh(first.RefreshToken), HttpStatusCode.Unauthorized, "AUTH_REFRESH_TOKEN_REVOKED");
        Assert.Equal(HttpStatusCode.OK, (await Refresh(second.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Rotated_access_token_authenticates_logout_and_replacement_cannot_refresh_afterwards()
    {
        var original = await Login();
        var response = await Refresh(original.RefreshToken);
        var rotated = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        var logout = await Logout(rotated.AccessToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("", await logout.Content.ReadAsStringAsync());
        await AssertProblem(await Refresh(rotated.RefreshToken), HttpStatusCode.Unauthorized, "AUTH_REFRESH_TOKEN_REVOKED");
    }

    [Fact]
    public async Task Simultaneous_refresh_requests_return_one_pair_and_one_rejection()
    {
        var original = await Login();
        var responses = await Task.WhenAll(Refresh(original.RefreshToken), Refresh(original.RefreshToken));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        await AssertProblem(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized),
            HttpStatusCode.Unauthorized, "AUTH_REFRESH_TOKEN_REVOKED");
        await using var db = _factory.NewContext();
        Assert.Equal(2, await db.RefreshTokens.CountAsync());
        Assert.Single(await db.RefreshTokens.Where(t => !t.IsRevoked).ToListAsync());
    }

    [Fact]
    public async Task Refresh_uses_current_user_roles_in_the_existing_jwt_service()
    {
        var original = await Login();
        using var scope = _factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Author"))).Succeeded);
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(_userId))!, "Author")).Succeeded);
        var response = await Refresh(original.RefreshToken);
        var rotated = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(rotated.AccessToken).Claims;
        Assert.Contains(claims, c => (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "Author");
    }

    [Fact]
    public async Task Expiry_boundary_is_rejected_without_issuing_replacement()
    {
        var tokens = await Login();
        _factory.Clock.Now = _factory.Clock.Now.AddDays(7);
        await AssertProblem(await Refresh(tokens.RefreshToken), HttpStatusCode.Unauthorized, "AUTH_REFRESH_TOKEN_EXPIRED");
        await using var db = _factory.NewContext();
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
        Assert.False((await db.RefreshTokens.SingleAsync()).IsRevoked);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.BadRequest, "VALIDATION_ERROR")]
    [InlineData(" ", HttpStatusCode.BadRequest, "VALIDATION_ERROR")]
    [InlineData("unknown", HttpStatusCode.Unauthorized, "AUTH_TOKEN_INVALID")]
    public async Task Refresh_rejects_missing_and_unknown_tokens(string? token, HttpStatusCode status, string code) =>
        await AssertProblem(await Refresh(token), status, code);

    [Fact]
    public async Task Duplicate_legacy_hash_is_rejected_without_500_or_rotation()
    {
        var tokens = await Login();
        await using var db = _factory.NewContext();
        var original = await db.RefreshTokens.AsNoTracking().SingleAsync();
        db.RefreshTokens.Add(new RefreshToken
        {
            Token = original.Token, UserId = original.UserId, ExpiresAt = original.ExpiresAt
        });
        await db.SaveChangesAsync();

        await AssertProblem(await Refresh(tokens.RefreshToken), HttpStatusCode.Unauthorized, "AUTH_TOKEN_INVALID");
        Assert.Equal(2, await db.RefreshTokens.CountAsync());
        Assert.All(await db.RefreshTokens.AsNoTracking().ToListAsync(), token => Assert.False(token.IsRevoked));
    }

    [Theory]
    [InlineData("plaintext", false)]
    [InlineData("lowercase-hash", false)]
    [InlineData("uppercase-hash", true)]
    public async Task Legacy_token_lookup_only_accepts_the_current_hash_encoding(string storage, bool accepted)
    {
        var tokens = await Login();
        var hash = new RefreshTokenGenerator().Hash(tokens.RefreshToken);
        var stored = storage switch
        {
            "plaintext" => tokens.RefreshToken,
            "lowercase-hash" => hash.ToLowerInvariant(),
            _ => hash
        };
        await using var db = _factory.NewContext();
        await db.RefreshTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.Token, stored));

        var response = await Refresh(tokens.RefreshToken);
        if (accepted)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        else
        {
            await AssertProblem(response, HttpStatusCode.Unauthorized, "AUTH_TOKEN_INVALID");
            var original = await db.RefreshTokens.AsNoTracking().SingleAsync();
            Assert.Equal(stored, original.Token); // No automatic backfill or plaintext fallback.
            Assert.False(original.IsRevoked);
        }
    }

    [Fact]
    public async Task Failed_login_does_not_issue_refresh_token()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var db = _factory.NewContext();
        Assert.Empty(await db.RefreshTokens.ToListAsync());
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("locked")]
    [InlineData("deleted")]
    public async Task Refresh_rejects_user_who_is_no_longer_allowed(string state)
    {
        var tokens = await Login();
        await using var db = _factory.NewContext();
        if (state == "deleted")
            await db.Users.Where(u => u.Id == _userId).ExecuteDeleteAsync();
        else if (state == "disabled")
            await db.Users.Where(u => u.Id == _userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        else
            await db.Users.Where(u => u.Id == _userId).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnabled, true).SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddHours(1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_current_token_is_idempotent_and_leaves_other_sessions_active()
    {
        var tokens = await Login();
        var otherSession = await Login();
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(tokens.AccessToken, tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(tokens.AccessToken, tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(tokens.AccessToken, "unknown")).StatusCode);
        await AssertProblem(await Refresh(tokens.RefreshToken), HttpStatusCode.Unauthorized, "AUTH_REFRESH_TOKEN_REVOKED");
        Assert.Equal(HttpStatusCode.OK, (await Refresh(otherSession.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_cannot_revoke_another_users_token()
    {
        var tokens = await Login();
        var foreignAccess = SignedAccess("other-user", DateTime.UtcNow.AddMinutes(5));
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(foreignAccess, tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Refresh(tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Expired_access_can_logout_only_with_active_owned_refresh_token()
    {
        var tokens = await Login();
        var expired = SignedAccess(_userId, DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(expired, "unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(expired, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(SignedAccess("other-user", DateTime.UtcNow.AddMinutes(-1)), tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(expired, tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(expired, tokens.RefreshToken)).StatusCode);
        await AssertProblem(await Refresh(tokens.RefreshToken), HttpStatusCode.Unauthorized, "AUTH_REFRESH_TOKEN_REVOKED");
    }

    [Theory]
    [InlineData("expired-refresh")]
    [InlineData("disabled-user")]
    [InlineData("invalid-signature")]
    [InlineData("invalid-issuer")]
    [InlineData("invalid-audience")]
    [InlineData("missing-access")]
    public async Task Logout_rejects_invalid_credentials_without_revocation(string scenario)
    {
        var tokens = await Login();
        var access = SignedAccess(_userId, DateTime.UtcNow.AddMinutes(-1),
            key: scenario == "invalid-signature" ? AuthApiFactory.Key + "wrong" : AuthApiFactory.Key,
            issuer: scenario == "invalid-issuer" ? "wrong" : "culinary-tests",
            audience: scenario == "invalid-audience" ? "wrong" : "culinary-tests");
        if (scenario == "expired-refresh") _factory.Clock.Now = _factory.Clock.Now.AddDays(8);
        await using var db = _factory.NewContext();
        if (scenario == "disabled-user")
            await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(scenario == "missing-access" ? null : access, tokens.RefreshToken)).StatusCode);
        Assert.False((await db.RefreshTokens.AsNoTracking().SingleAsync()).IsRevoked);
    }

    private static string SignedAccess(string userId, DateTime expires, string key = AuthApiFactory.Key,
        string issuer = "culinary-tests", string audience = "culinary-tests") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience,
            [new Claim(ClaimTypes.NameIdentifier, userId)], expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
}
