using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.API.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Liveness_is_self_only_even_when_dependencies_fail()
    {
        await using var host = await HealthHost.StartAsync(
            HealthStatus.Unhealthy, HealthStatus.Unhealthy, HealthStatus.Unhealthy);

        var response = await host.Client.GetAsync("/health/live");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body!.Status);
        Assert.Equal(new Dictionary<string, string> { ["self"] = "Healthy" }, body.Components);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Unhealthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HealthStatus.Healthy, HealthStatus.Healthy, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HealthStatus.Healthy, HealthStatus.Unhealthy, HealthStatus.Healthy, HttpStatusCode.ServiceUnavailable)]
    public async Task Readiness_depends_on_postgresql_and_redis_only(
        HealthStatus postgresql,
        HealthStatus redis,
        HealthStatus minio,
        HttpStatusCode expected)
    {
        await using var host = await HealthHost.StartAsync(postgresql, redis, minio);

        var response = await host.Client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(["postgresql", "redis"], body!.Components.Keys.Order());
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Healthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HealthStatus.Healthy, HealthStatus.Healthy, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HealthStatus.Healthy, HealthStatus.Unhealthy, HealthStatus.Healthy, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
    public async Task Aggregate_depends_on_all_external_components(
        HealthStatus postgresql,
        HealthStatus redis,
        HealthStatus minio,
        HttpStatusCode expected)
    {
        await using var host = await HealthHost.StartAsync(postgresql, redis, minio);

        var response = await host.Client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(["minio", "postgresql", "redis"], body!.Components.Keys.Order());
    }

    [Fact]
    public async Task Response_exposes_only_status_and_component_statuses()
    {
        await using var host = await HealthHost.StartAsync(
            HealthStatus.Unhealthy, HealthStatus.Healthy, HealthStatus.Healthy,
            "Host=secret-db;Password=SECRET-DB-PASSWORD");

        var response = await host.Client.GetAsync("/health");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(["components", "status"], document.RootElement.EnumerateObject()
            .Select(property => property.Name).Order());
        Assert.DoesNotContain("secret-db", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET-DB-PASSWORD", json, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_anonymous(string path)
    {
        await using var host = await HealthHost.StartAsync(
            HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Healthy);

        var response = await host.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cooperative_check_is_cancelled_at_its_configured_timeout()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await HealthHost.StartWithBlockingPostgresqlAsync(cancelled);
        var stopwatch = Stopwatch.StartNew();

        var response = await host.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Production_dependency_checks_keep_two_second_timeouts_and_expected_tags()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCulinaryHealthChecks();
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.ToDictionary(registration => registration.Name);

        Assert.Equal(["live"], registrations["self"].Tags.Order());
        Assert.Equal(["aggregate", "ready"], registrations["postgresql"].Tags.Order());
        Assert.Equal(["aggregate", "ready"], registrations["redis"].Tags.Order());
        Assert.Equal(["aggregate"], registrations["minio"].Tags.Order());
        Assert.Equal(TimeSpan.FromSeconds(2), registrations["postgresql"].Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), registrations["redis"].Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), registrations["minio"].Timeout);
    }

    private sealed record HealthResponse(string Status, Dictionary<string, string> Components);

    private sealed class FixedHealthCheck(HealthStatus status, string? sentinel = null) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(status == HealthStatus.Healthy
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    sentinel,
                    new InvalidOperationException(sentinel),
                    new Dictionary<string, object> { ["configuration"] = sentinel ?? "SECRET-CONFIG" }));
    }

    private sealed class BlockingHealthCheck(TaskCompletionSource cancelled) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class HealthHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static Task<HealthHost> StartAsync(
            HealthStatus postgresql,
            HealthStatus redis,
            HealthStatus minio,
            string? sentinel = null) => StartAsync(services => services
                .AddHealthChecks()
                .AddCheck("self", new FixedHealthCheck(HealthStatus.Healthy), tags: ["live"])
                .AddCheck("postgresql", new FixedHealthCheck(postgresql, sentinel), tags: ["ready", "aggregate"])
                .AddCheck("redis", new FixedHealthCheck(redis), tags: ["ready", "aggregate"])
                .AddCheck("minio", new FixedHealthCheck(minio), tags: ["aggregate"]));

        public static Task<HealthHost> StartWithBlockingPostgresqlAsync(TaskCompletionSource cancelled) =>
            StartAsync(services => services
                .AddHealthChecks()
                .AddCheck("self", new FixedHealthCheck(HealthStatus.Healthy), tags: ["live"])
                .AddCheck("postgresql", new BlockingHealthCheck(cancelled), tags: ["ready", "aggregate"],
                    timeout: TimeSpan.FromMilliseconds(30))
                .AddCheck("redis", new FixedHealthCheck(HealthStatus.Healthy), tags: ["ready", "aggregate"])
                .AddCheck("minio", new FixedHealthCheck(HealthStatus.Healthy), tags: ["aggregate"]));

        private static async Task<HealthHost> StartAsync(Action<IServiceCollection> configure)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            configure(builder.Services);
            var app = builder.Build();
            app.MapCulinaryHealthChecks();
            await app.StartAsync();
            return new HealthHost(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
