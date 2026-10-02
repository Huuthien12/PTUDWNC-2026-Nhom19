using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Liveness_does_not_require_external_dependencies()
    {
        await using var factory = new AuthApiFactory();
        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }

    private sealed record HealthResponse(string Status);
}
