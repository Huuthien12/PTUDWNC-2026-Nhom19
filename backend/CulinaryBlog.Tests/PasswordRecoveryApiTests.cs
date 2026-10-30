using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class PasswordRecoveryApiTests : IAsyncLifetime
{
    private const string Email = "password-recovery@test.local";
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
        Assert.True((await users.CreateAsync(new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            FullName = "Recovery User"
        }, OldPassword)).Succeeded);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Forgot_password_is_indistinguishable_for_known_and_unknown_accounts()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser
            {
                UserName = "inactive@test.local",
                Email = "inactive@test.local",
                FullName = "Inactive User",
                IsActive = false
            }, OldPassword)).Succeeded);
        }

        using var known = await ForgotAsync(Email);
        using var unknown = await ForgotAsync("unknown@test.local");
        using var inactive = await ForgotAsync("inactive@test.local");

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(known.StatusCode, inactive.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(await known.Content.ReadAsStringAsync(), await inactive.Content.ReadAsStringAsync());
        var delivered = await _factory.Emails.WaitForDeliveryAsync();
        Assert.Single(_factory.Emails.PasswordResets);
        Assert.Equal(Email, delivered.Email);
        Assert.DoesNotContain(delivered.Token,
            await known.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forgot_password_rejects_invalid_email_without_sending()
    {
        using var response = await ForgotAsync("not-an-email");
        await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Empty(_factory.Emails.PasswordResets);
    }

    [Fact]
    public async Task Forgot_password_hides_email_delivery_failure()
    {
        _factory.Emails.FailPasswordResetDelivery = true;
        using var response = await ForgotAsync(Email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await _factory.Emails.WaitForDeliveryAttemptAsync();
        Assert.Empty(_factory.Emails.PasswordResets);
    }

    [Fact]
    public async Task Forgot_password_returns_before_smtp_delivery_completes()
    {
        _factory.Emails.BlockPasswordResetDelivery = true;
        var responseTask = ForgotAsync(Email);
        await _factory.Emails.WaitForDeliveryAttemptAsync();

        try
        {
            using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Empty(_factory.Emails.PasswordResets);
        }
        finally
        {
            _factory.Emails.ReleaseDelivery();
        }

        Assert.Equal(Email, (await _factory.Emails.WaitForDeliveryAsync()).Email);
    }

    [Fact]
    public async Task Forgot_password_hides_a_full_email_queue()
    {
        await using var factory = new AuthApiFactory { RejectPasswordResetEmails = true };
        using var client = factory.CreateClient();
        await using (var db = factory.NewContext())
            await db.Database.EnsureCreatedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser
            {
                UserName = Email,
                Email = Email,
                FullName = "Recovery User"
            }, OldPassword)).Succeeded);
        }

        using var known = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = Email });
        using var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "unknown@test.local" });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(1, factory.RejectedEmails.Attempts);
    }

    [Fact]
    public void Password_reset_email_queue_is_bounded_and_non_blocking()
    {
        var queue = new PasswordResetEmailQueue(capacity: 1);
        Assert.True(queue.TryEnqueue(new(Email, "Recovery User", "first-token")));
        Assert.False(queue.TryEnqueue(new(Email, "Recovery User", "second-token")));
    }

    [Fact]
    public async Task Forgot_email_reset_then_login_works_as_a_complete_recovery_flow()
    {
        using var forgot = await ForgotAsync(Email);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        var recoveryEmail = await _factory.Emails.WaitForDeliveryAsync();
        Assert.Equal(Email, recoveryEmail.Email);
        Assert.False(string.IsNullOrWhiteSpace(recoveryEmail.Token));

        using var reset = await ResetAsync(recoveryEmail.Email, recoveryEmail.Token, NewPassword, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var oldPasswordLogin = await LoginAsync(OldPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
        using var newPasswordLogin = await LoginAsync(NewPassword);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_password_changes_login_password_and_revokes_refresh_sessions()
    {
        using var login = await LoginAsync(OldPassword);
        var session = (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        var token = await RequestTokenAsync();

        using var reset = await ResetAsync(Email, token, NewPassword, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewPassword)).StatusCode);
        using var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Reset_password_rolls_back_when_refresh_revocation_fails()
    {
        using var login = await LoginAsync(OldPassword);
        var session = (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        var token = await RequestTokenAsync();

        _factory.FailRefreshRevocation = true;
        using var reset = await ResetAsync(Email, token, NewPassword, NewPassword);
        Assert.Equal(HttpStatusCode.InternalServerError, reset.StatusCode);
        _factory.FailRefreshRevocation = false;

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewPassword)).StatusCode);
        using var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Reset_password_rejects_invalid_malformed_unknown_and_replayed_tokens_safely()
    {
        var token = await RequestTokenAsync();
        await AssertResetInvalid(await ResetAsync(Email, "invalid-token", NewPassword, NewPassword));
        await AssertResetInvalid(await ResetAsync(Email, "*", NewPassword, NewPassword));
        await AssertResetInvalid(await ResetAsync("unknown@test.local", token, NewPassword, NewPassword));

        using var success = await ResetAsync(Email, token, NewPassword, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
        await AssertResetInvalid(await ResetAsync(Email, token, OldPassword, OldPassword));
    }

    [Fact]
    public async Task Reset_password_validates_confirmation_and_identity_password_policy()
    {
        var token = await RequestTokenAsync();
        await AssertProblem(await ResetAsync(Email, token, NewPassword, "different-password"),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await AssertProblem(await ResetAsync(Email, token, "weak", "weak"),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Forgot_password_uses_existing_auth_rate_limit_bucket()
    {
        await using var factory = new AuthApiFactory { EnableRateLimiting = true };
        using var client = factory.CreateClient();
        await using (var db = factory.NewContext())
            await db.Database.EnsureCreatedAsync();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var accepted = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
                new { email = "unknown@test.local" });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "unknown@test.local" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        await AssertProblem(rejected, HttpStatusCode.TooManyRequests, "RATE_LIMIT_EXCEEDED");
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });

    private Task<HttpResponseMessage> LoginAsync(string password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password });

    private Task<HttpResponseMessage> ResetAsync(
        string email,
        string token,
        string newPassword,
        string confirmNewPassword) =>
        _client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { email, token, newPassword, confirmNewPassword });

    private async Task<string> RequestTokenAsync()
    {
        using var response = await ForgotAsync(Email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await _factory.Emails.WaitForDeliveryAsync()).Token;
    }

    private static Task AssertResetInvalid(HttpResponseMessage response) =>
        AssertProblem(response, HttpStatusCode.BadRequest, "RESET_PASSWORD_INVALID_OR_EXPIRED");

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(type, problem.GetProperty("type").GetString());
        }
    }
}
