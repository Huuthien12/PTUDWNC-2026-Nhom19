using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CulinaryBlog.API.Health;

public static class HealthEndpointExtensions
{
    public static IServiceCollection AddCulinaryHealthChecks(this IServiceCollection services) => services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready", "aggregate"], timeout: TimeSpan.FromSeconds(2))
        .AddCheck<RedisHealthCheck>("redis", tags: ["ready", "aggregate"], timeout: TimeSpan.FromSeconds(2))
        .AddCheck<MinioHealthCheck>("minio", tags: ["aggregate"], timeout: TimeSpan.FromSeconds(2))
        .Services;

    public static void MapCulinaryHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live"), ResponseWriter = WriteResponse });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready"), ResponseWriter = WriteResponse });
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("aggregate"), ResponseWriter = WriteResponse });
    }

    private static Task WriteResponse(HttpContext context, HealthReport report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        components = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
    });
}

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) => await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
}

public sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try { await cache.GetAsync("health:probe", cancellationToken); return HealthCheckResult.Healthy(); }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}

public sealed class MinioHealthCheck(IFileStorageService storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) => await storage.IsAvailableAsync(cancellationToken)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
}
