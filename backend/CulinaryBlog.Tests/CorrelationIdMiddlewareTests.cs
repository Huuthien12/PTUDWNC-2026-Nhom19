using CulinaryBlog.API.Middleware;
using Microsoft.AspNetCore.Http;
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
