using System.Text.Json;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class GlobalExceptionMiddlewareTests
{
    private const string SensitiveSentinel = "SECRET-RESET-TOKEN-DO-NOT-LOG";

    [Theory]
    [InlineData(404, "CATEGORY_NOT_FOUND")]
    [InlineData(409, "CATEGORY_NAME_EXISTS")]
    [InlineData(400, "VALIDATION_ERROR")]
    [InlineData(400, "RECIPE_PUBLISH_INCOMPLETE")]
    [InlineData(403, "RECIPE_FORBIDDEN")]
    [InlineData(422, "RECIPE_CONCURRENCY_CONFLICT")]
    [InlineData(401, "AUTH_TOKEN_INVALID")]
    [InlineData(503, "CACHE_UNAVAILABLE")]
    public async Task Maps_expected_exceptions_to_problem_details(
        int expectedStatus,
        string expectedCode)
    {
        Exception exception = expectedCode switch
        {
            "CATEGORY_NOT_FOUND" => new NotFoundException(expectedCode, "Missing category."),
            "CATEGORY_NAME_EXISTS" => new ConflictException(expectedCode, "Duplicate category."),
            "VALIDATION_ERROR" => new ValidationException([new ValidationFailure("Name", "Name is required.")]),
            "RECIPE_PUBLISH_INCOMPLETE" => new BusinessRuleException(expectedCode, "Recipe is incomplete."),
            "RECIPE_FORBIDDEN" => new ForbiddenException(expectedCode, "Action is not allowed."),
            "RECIPE_CONCURRENCY_CONFLICT" => new DbUpdateConcurrencyException(),
            "CACHE_UNAVAILABLE" => new ServiceUnavailableException(expectedCode, "Cache unavailable."),
            _ => new UnauthorizedAccessException("Do not expose this.")
        };

        var (context, body, logger) = await InvokeAsync(exception);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("type").GetString());
        Assert.Equal("test-trace-id", document.RootElement.GetProperty("traceId").GetString());

        if (expectedCode == "VALIDATION_ERROR")
        {
            Assert.True(document.RootElement.TryGetProperty("errors", out _));
        }

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Maps_unexpected_exceptions_to_safe_problem_details()
    {
        var (context, body, logger) = await InvokeAsync(ThrowSensitiveException);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.DoesNotContain(SensitiveSentinel, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(nameof(ThrowSensitiveException), body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("INTERNAL_SERVER_ERROR", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("test-trace-id", document.RootElement.GetProperty("traceId").GetString());
        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(nameof(InvalidOperationException), error.Properties["ExceptionType"]);
        var stackTrace = Assert.IsType<string>(error.Properties["ExceptionStackTrace"]);
        Assert.Contains(nameof(ThrowSensitiveException), stackTrace);
        Assert.Null(error.Exception);
        Assert.DoesNotContain(SensitiveSentinel, logger.AllText);
    }

    private static async Task<(DefaultHttpContext Context, string Body, RecordingLogger<GlobalExceptionMiddleware> Logger)>
        InvokeAsync(Exception exception)
        => await InvokeAsync(_ => Task.FromException(exception));

    private static async Task<(DefaultHttpContext Context, string Body, RecordingLogger<GlobalExceptionMiddleware> Logger)>
        InvokeAsync(RequestDelegate next)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test";
        context.TraceIdentifier = "test-trace-id";
        context.Response.Body = new MemoryStream();

        var logger = new RecordingLogger<GlobalExceptionMiddleware>();
        var middleware = new GlobalExceptionMiddleware(next, logger);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return (context, await reader.ReadToEndAsync(), logger);
    }

    private static Task ThrowSensitiveException(HttpContext _)
    {
        throw new InvalidOperationException(SensitiveSentinel);
    }
}
