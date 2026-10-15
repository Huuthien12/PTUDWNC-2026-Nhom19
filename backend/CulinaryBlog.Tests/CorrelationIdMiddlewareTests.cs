using CulinaryBlog.API.Middleware;
using CulinaryBlog.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Missing_id_is_generated_and_returned()
    {
        var context = new DefaultHttpContext();
        string? downstreamId = null;
        var middleware = new CorrelationIdMiddleware(
            nextContext =>
            {
                downstreamId = nextContext.Items[CorrelationIdMiddleware.ItemKey] as string;
                return Task.CompletedTask;
            },
            new RecordingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        var responseId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParseExact(responseId, "N", out _));
        Assert.Equal(responseId, downstreamId);
    }

    [Fact]
    public async Task Valid_client_id_is_preserved()
    {
        const string suppliedId = "client-safe_123.example";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = suppliedId;
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Assert.Equal(suppliedId, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal(suppliedId, context.Items[CorrelationIdMiddleware.ItemKey]);
    }

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public async Task Unsafe_client_id_is_replaced(string unsafeId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = unsafeId;
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        var effectiveId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.NotEqual(unsafeId, effectiveId);
        Assert.True(Guid.TryParseExact(effectiveId, "N", out _));
    }

    [Fact]
    public async Task Multiple_client_ids_are_replaced_with_one_generated_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] =
            new StringValues(["value-one", "value-two"]);
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        var responseIds = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        Assert.Equal(1, responseIds.Count);
        Assert.DoesNotContain(responseIds.ToString(), new[] { "value-one", "value-two" });
        Assert.True(Guid.TryParseExact(responseIds.ToString(), "N", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("lab8-test-123")]
    public async Task Handled_error_response_returns_effective_id(string? suppliedId)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(services => services.AddLogging())
                .Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();
                    app.UseMiddleware<GlobalExceptionMiddleware>();
                    app.Run(_ => Task.FromException(
                        new NotFoundException("TEST_NOT_FOUND", "Missing.")));
                }))
            .StartAsync();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test");
        if (suppliedId is not null)
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedId);

        using var response = await client.SendAsync(request);

        Assert.Equal(StatusCodes.Status404NotFound, (int)response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        var responseIds = values.ToArray();
        var effectiveId = Assert.Single(responseIds);
        if (suppliedId is null)
            Assert.True(Guid.TryParseExact(effectiveId, "N", out _));
        else
            Assert.Equal(suppliedId, effectiveId);
    }

    [Fact]
    public async Task Concurrent_requests_keep_distinct_ids()
    {
        var logger = new RecordingLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(
            async context =>
            {
                await Task.Yield();
                Assert.Equal(
                    context.Items[CorrelationIdMiddleware.ItemKey],
                    context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
            },
            logger);
        var contexts = Enumerable.Range(0, 32).Select(_ => new DefaultHttpContext()).ToArray();

        await Task.WhenAll(contexts.Select(middleware.InvokeAsync));

        var ids = contexts
            .Select(context => context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString())
            .ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ids.Order(StringComparer.Ordinal),
            logger.Scopes.Select(scope => scope[CorrelationIdMiddleware.ItemKey]?.ToString())
                .Order(StringComparer.Ordinal));
    }

    public static IEnumerable<object[]> InvalidIds()
    {
        yield return ["bad\r\nid"];
        yield return ["bad\u0001id"];
        yield return [new string('a', 65)];
        yield return ["contains space"];
    }

    private static CorrelationIdMiddleware CreateMiddleware() => new(
        _ => Task.CompletedTask,
        new RecordingLogger<CorrelationIdMiddleware>());
}
