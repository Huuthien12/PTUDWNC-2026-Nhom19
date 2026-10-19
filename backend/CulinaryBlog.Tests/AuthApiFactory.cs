using System.Collections.Concurrent;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Authentication.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Repositories;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CulinaryBlog.Tests;

// Only auth entities are mapped in SQLite; production still uses the full PostgreSQL model.
public sealed class AuthTestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Ignore<Recipe>();
        builder.Ignore<RecipeNutrition>();
        builder.Ignore<RecipeIngredient>();
        builder.Ignore<RecipeStep>();
        builder.Ignore<RecipeImage>();
        builder.Ignore<Category>();
        builder.Entity<ApplicationUser>().Ignore(u => u.Recipes);
    }
}

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class RecordingWelcomeEmailQueue : IWelcomeEmailQueue
{
    public List<(string Email, string FullName)> Messages { get; } = [];

    public Task EnqueueAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Messages.Add((email, fullName));
        return Task.CompletedTask;
    }
}

public sealed class RecordingEmailService : IEmailService
{
    private readonly TaskCompletionSource _deliveryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseDelivery = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<(string Email, string FullName, string Token)> _deliveryCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentQueue<(string Email, string FullName, string Token)> PasswordResets { get; } = [];
    public bool FailPasswordResetDelivery { get; set; }
    public bool BlockPasswordResetDelivery { get; set; }

    public Task SendWelcomeEmailAsync(
        string email,
        string fullName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task SendPasswordResetEmailAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _deliveryStarted.TrySetResult();
        if (BlockPasswordResetDelivery)
            await _releaseDelivery.Task.WaitAsync(cancellationToken);
        if (FailPasswordResetDelivery) throw new InvalidOperationException("Test email failure.");
        var message = (email, fullName, token);
        PasswordResets.Enqueue(message);
        _deliveryCompleted.TrySetResult(message);
    }

    public Task WaitForDeliveryAttemptAsync() =>
        _deliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public Task<(string Email, string FullName, string Token)> WaitForDeliveryAsync() =>
        _deliveryCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void ReleaseDelivery() => _releaseDelivery.TrySetResult();
}

public sealed class RejectingPasswordResetEmailQueue : IPasswordResetEmailQueue
{
    public int Attempts { get; private set; }

    public bool TryEnqueue(PasswordResetEmailMessage message)
    {
        Attempts++;
        return false;
    }
}

public sealed class ControllableRefreshTokenStore(
    AppDbContext context,
    Func<bool> failRevocation) : IRefreshTokenStore
{
    private readonly RefreshTokenStore _inner = new(context);

    public Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        _inner.FindAsync(tokenHash, cancellationToken);

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken) =>
        _inner.AddAsync(token, cancellationToken);

    public Task<bool> TryRotateAsync(Guid id, RefreshToken replacement, DateTime now,
        CancellationToken cancellationToken) =>
        _inner.TryRotateAsync(id, replacement, now, cancellationToken);

    public Task RevokeAllAsync(string userId, DateTime now, CancellationToken cancellationToken) =>
        failRevocation()
            ? Task.FromException(new InvalidOperationException("Test refresh revocation failure."))
            : _inner.RevokeAllAsync(userId, now, cancellationToken);

    public Task<bool> RevokeAsync(string tokenHash, string userId, DateTime now, bool requireActive,
        CancellationToken cancellationToken) =>
        _inner.RevokeAsync(tokenHash, userId, now, requireActive, cancellationToken);
}

public sealed class FakeGoogleCredentialVerifier : IGoogleCredentialVerifier
{
    public GoogleIdentity? Identity { get; set; }
    public string? AuthorizationCode { get; private set; }
    public string? CodeVerifier { get; private set; }
    public Task<GoogleIdentity?> VerifyAsync(string? idToken, string? authorizationCode, string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        AuthorizationCode = authorizationCode;
        CodeVerifier = codeVerifier;
        return Task.FromResult(Identity);
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    // Public, test-only signing material. Never loaded from user secrets.
    public const string Key = "culinary-tests-only-signing-key-64-characters-012345678901234567890";
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"culinary-auth-tests-{Guid.NewGuid():N}.db");
    public TestClock Clock { get; } = new();
    public RecordingWelcomeEmailQueue WelcomeEmails { get; } = new();
    public RecordingEmailService Emails { get; } = new();
    public RejectingPasswordResetEmailQueue RejectedEmails { get; } = new();
    public FakeGoogleCredentialVerifier GoogleVerifier { get; } = new();
    public RecordingRecipeInvalidator RecipeInvalidator { get; } = new();
    public bool EnableRateLimiting { get; init; }
    public bool RejectPasswordResetEmails { get; init; }
    public bool FailRefreshRevocation { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=1;Database=Unused;Username=test");
        builder.UseSetting("Jwt:Key", Key);
        builder.UseSetting("Jwt:Issuer", "culinary-tests");
        builder.UseSetting("Jwt:Audience", "culinary-tests");
        builder.UseSetting("Jwt:ExpirationMinutes", "15");
        builder.UseSetting("Email:Smtp:EnableSsl", "true");
        builder.UseSetting("PasswordReset:FrontendBaseUrl", "https://frontend.test");
        builder.UseSetting("RateLimiting:Enabled", EnableRateLimiting.ToString());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddScoped<AppDbContext>(_ => NewContext());
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IWelcomeEmailQueue>();
            services.AddSingleton<IWelcomeEmailQueue>(WelcomeEmails);
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
            if (RejectPasswordResetEmails)
            {
                services.RemoveAll<IPasswordResetEmailQueue>();
                services.AddSingleton<IPasswordResetEmailQueue>(RejectedEmails);
            }
            services.RemoveAll<IRefreshTokenStore>();
            services.AddScoped<IRefreshTokenStore>(services => new ControllableRefreshTokenStore(
                services.GetRequiredService<AppDbContext>(), () => FailRefreshRevocation));
            services.RemoveAll<IGoogleCredentialVerifier>();
            services.AddSingleton<IGoogleCredentialVerifier>(GoogleVerifier);
            // This fixture intentionally omits Recipe entities; profile cache signals use a recording fake.
            services.RemoveAll<IRecipeCacheInvalidator>();
            services.AddSingleton<IRecipeCacheInvalidator>(RecipeInvalidator);
        });
    }

    public AuthTestDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={DatabasePath};Pooling=False").Options);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Delete only the uniquely named database created by this test fixture.
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }
}
