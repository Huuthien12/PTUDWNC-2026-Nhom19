using System.Reflection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CulinaryBlog.API.Observability;

public static class OpenTelemetryExtensions
{
    public const string ServiceName = "CulinaryBlog.API";

    public static IServiceCollection AddCulinaryBlogOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var assembly = typeof(OpenTelemetryExtensions).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        var endpointValue = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var otlpEndpoint = string.IsNullOrWhiteSpace(endpointValue)
            ? null
            : new Uri(endpointValue, UriKind.Absolute);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, serviceVersion: version)
                .AddAttributes([new("deployment.environment.name", environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();
                if (otlpEndpoint is not null)
                    tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (otlpEndpoint is not null)
                    metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            });

        return services;
    }
}
