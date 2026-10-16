using System.Diagnostics;
using CulinaryBlog.API.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace CulinaryBlog.Tests;

[Collection("OpenTelemetry serial")]
public sealed class OpenTelemetryTests
{
    private const string ResetToken = "SECRET-RESET-TOKEN-OTEL-DO-NOT-EXPORT";

    [Fact]
    public async Task Server_span_has_http_metadata_safe_resource_and_no_reset_token()
    {
        await using var host = await TelemetryHost.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/auth/reset-password?token={ResetToken}");

        var response = await host.Client.SendAsync(request);
        host.Flush();

        Assert.Equal(StatusCodes.Status204NoContent, (int)response.StatusCode);
        var server = Assert.Single(host.Activities, activity => activity.Kind == ActivityKind.Server);
        Assert.Equal("POST", server.GetTagItem("http.request.method"));
        Assert.Equal("/api/v1/auth/reset-password", server.GetTagItem("http.route"));
        Assert.Equal(204, server.GetTagItem("http.response.status_code"));
        Assert.Contains(host.Resource!.Attributes,
            attribute => attribute.Key == "service.name" && Equals(attribute.Value, OpenTelemetryExtensions.ServiceName));
        Assert.Contains(host.Resource.Attributes,
            attribute => attribute.Key == "deployment.environment.name" && Equals(attribute.Value, "Testing"));
        Assert.DoesNotContain(ResetToken, host.ExportText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Server_span_continues_valid_w3c_traceparent_without_changing_correlation_id()
    {
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        await using var host = await TelemetryHost.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.TryAddWithoutValidation("traceparent", $"00-{traceId}-b7ad6b7169203331-01");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "independent-correlation-id");

        var response = await host.Client.SendAsync(request);
        host.Flush();

        var server = Assert.Single(host.Activities, activity => activity.Kind == ActivityKind.Server);
        Assert.Equal(traceId, server.TraceId.ToHexString());
        Assert.Equal("independent-correlation-id", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Missing_otlp_endpoint_does_not_require_a_collector()
    {
        await using var host = await TelemetryHost.StartAsync();

        var normal = await host.Client.GetAsync("/probe");
        var health = await host.Client.GetAsync("/health/live");

        Assert.Equal(StatusCodes.Status200OK, (int)normal.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (int)health.StatusCode);
    }

    [Fact]
    public async Task Unavailable_otlp_collector_does_not_break_business_requests()
    {
        await using var host = await TelemetryHost.StartAsync("http://127.0.0.1:1");

        var response = await host.Client.GetAsync("/probe");

        Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
    }

    [Fact]
    public void Explicitly_malformed_otlp_endpoint_fails_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "not-a-valid-uri"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<UriFormatException>(() =>
            services.AddCulinaryBlogOpenTelemetry(configuration, new TestEnvironment()));
    }

    [Fact]
    public async Task Ef_parameter_value_is_not_exported()
    {
        const string sentinel = "secret-user@example.invalid";
        var activities = new List<Activity>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCulinaryBlogOpenTelemetry(configuration, new TestEnvironment());
        services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(activities));
        await using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new TelemetryDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new TelemetryUser { Email = sentinel });
        await db.SaveChangesAsync();

        _ = await db.Users.SingleAsync(user => user.Email == sentinel);
        provider.GetRequiredService<TracerProvider>().ForceFlush();

        Assert.Contains(activities,
            activity => activity.Source.Name.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(sentinel, ActivityText(activities), StringComparison.Ordinal);
    }

    private static string ActivityText(IEnumerable<Activity> activities) => string.Join('\n', activities.SelectMany(activity =>
        new[] { activity.DisplayName, activity.StatusDescription }
            .Concat(activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
            .Concat(activity.Events.SelectMany(item => new[] { item.Name }
                .Concat(item.Tags.Select(tag => $"{tag.Key}={tag.Value}"))))
            .Concat(activity.Links.SelectMany(link =>
                link.Tags?.Select(tag => $"{tag.Key}={tag.Value}") ?? []))));

    private sealed class ResourceCaptureProcessor : BaseProcessor<Activity>
    {
        public Resource? Resource { get; private set; }
        public override void OnEnd(Activity data) => Resource ??= ParentProvider.GetResource();
    }

    private sealed class TelemetryHost(
        WebApplication app,
        HttpClient client,
        List<Activity> activities,
        ResourceCaptureProcessor resourceCapture) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public IReadOnlyCollection<Activity> Activities => activities;
        public Resource? Resource => resourceCapture.Resource;

        public static async Task<TelemetryHost> StartAsync(string? otlpEndpoint = null)
        {
            var activities = new List<Activity>();
            var resourceCapture = new ResourceCaptureProcessor();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlpEndpoint
            });
            builder.Services.AddCulinaryBlogOpenTelemetry(builder.Configuration, builder.Environment);
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
                .AddInMemoryExporter(activities)
                .AddProcessor(resourceCapture));
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Correlation-ID"] = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                    ?? Guid.NewGuid().ToString("N");
                await next();
            });
            app.MapPost("/api/v1/auth/reset-password", () => Results.NoContent());
            app.MapGet("/probe", () => Results.Ok());
            app.MapGet("/health/live", () => Results.Ok());
            await app.StartAsync();
            return new TelemetryHost(app, app.GetTestClient(), activities, resourceCapture);
        }

        public void Flush() => app.Services.GetRequiredService<TracerProvider>().ForceFlush();

        public string ExportText() => ActivityText(activities) + '\n' + string.Join('\n',
            Resource?.Attributes.Select(attribute => $"{attribute.Key}={attribute.Value}") ?? []);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
    {
        public DbSet<TelemetryUser> Users => Set<TelemetryUser>();
    }

    private sealed class TelemetryUser
    {
        public int Id { get; set; }
        public required string Email { get; set; }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CulinaryBlog.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

[CollectionDefinition("OpenTelemetry serial", DisableParallelization = true)]
public sealed class OpenTelemetrySerialCollection;
