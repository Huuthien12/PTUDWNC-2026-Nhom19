using System.Text;
using CulinaryBlog.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task Sensitive_headers_body_and_query_are_not_logged()
    {
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(
            _ => Task.CompletedTask,
            logger,
            new ManualTimeProvider());

        var login = CreateContext("/api/v1/auth/login", "?returnUrl=%2Fprofile");
        login.Request.Headers.Authorization = "Bearer authorization-secret";
        login.Request.Headers.Cookie = "refreshToken=cookie-secret";
        login.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"email\":\"member@example.test\",\"password\":\"password-secret\"}"));
        await middleware.InvokeAsync(login);

        var reset = CreateContext("/api/v1/auth/reset-password", "?token=reset-secret");
        await middleware.InvokeAsync(reset);

        Assert.Contains("/api/v1/auth/login", logger.AllText);
        Assert.Contains("/api/v1/auth/reset-password", logger.AllText);
        Assert.DoesNotContain("authorization-secret", logger.AllText);
        Assert.DoesNotContain("cookie-secret", logger.AllText);
        Assert.DoesNotContain("password-secret", logger.AllText);
        Assert.DoesNotContain("member@example.test", logger.AllText);
        Assert.DoesNotContain("reset-secret", logger.AllText);
        Assert.DoesNotContain("returnUrl", logger.AllText);
    }

    [Fact]
    public async Task Request_slower_than_threshold_is_warning_without_sleeping()
    {
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var clock = new ManualTimeProvider();
        var middleware = new RequestLoggingMiddleware(
            _ =>
            {
                clock.Advance(TimeSpan.FromMilliseconds(501));
                return Task.CompletedTask;
            },
            logger,
            clock);

        await middleware.InvokeAsync(CreateContext("/api/v1/categories"));

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Successful_health_request_is_debug()
    {
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(
            _ => Task.CompletedTask,
            logger,
            new ManualTimeProvider());

        await middleware.InvokeAsync(CreateContext("/health/ready"));

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Exception_already_classified_by_global_middleware_is_not_a_second_error()
    {
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(
            _ => Task.CompletedTask,
            logger,
            new ManualTimeProvider());
        var context = CreateContext("/api/v1/test");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Items[GlobalExceptionMiddleware.HandledExceptionItemKey] = true;

        await middleware.InvokeAsync(context);

        Assert.Equal(LogLevel.Information, Assert.Single(logger.Entries).Level);
    }

    private static DefaultHttpContext CreateContext(string path, string? query = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query ?? string.Empty);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.TraceIdentifier = "trace-safe";
        context.Items[CorrelationIdMiddleware.ItemKey] = "correlation-safe";
        return context;
    }
}
