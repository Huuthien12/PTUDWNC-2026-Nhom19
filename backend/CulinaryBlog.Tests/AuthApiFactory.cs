using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Authentication.Commands;
using CulinaryBlog.Domain.Entities;
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
    public FakeGoogleCredentialVerifier GoogleVerifier { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=1;Database=Unused;Username=test");
        builder.UseSetting("Jwt:Key", Key);
        builder.UseSetting("Jwt:Issuer", "culinary-tests");
        builder.UseSetting("Jwt:Audience", "culinary-tests");
        builder.UseSetting("Jwt:ExpirationMinutes", "15");
        builder.UseSetting("Email:Smtp:EnableSsl", "true");
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
            services.RemoveAll<IGoogleCredentialVerifier>();
            services.AddSingleton<IGoogleCredentialVerifier>(GoogleVerifier);
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
