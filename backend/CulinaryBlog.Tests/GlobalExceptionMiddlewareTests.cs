using System.Text.Json;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class GlobalExceptionMiddlewareTests
{
    [Theory]
    [InlineData(404, "CATEGORY_NOT_FOUND")]
    [InlineData(409, "CATEGORY_NAME_EXISTS")]
    [InlineData(400, "VALIDATION_ERROR")]
    [InlineData(400, "RECIPE_PUBLISH_INCOMPLETE")]
    [InlineData(403, "RECIPE_FORBIDDEN")]
    [InlineData(422, "RECIPE_CONCURRENCY_CONFLICT")]
    [InlineData(401, "AUTH_TOKEN_INVALID")]
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
            _ => new UnauthorizedAccessException("Do not expose this.")
        };

        var (context, body) = await InvokeAsync(exception);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("type").GetString());
        Assert.Equal("test-trace-id", document.RootElement.GetProperty("traceId").GetString());

        if (expectedCode == "VALIDATION_ERROR")
        {
            Assert.True(document.RootElement.TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public async Task Maps_unexpected_exceptions_to_safe_problem_details()
    {
        var (context, body) = await InvokeAsync(
            new InvalidOperationException("Database password: secret-value"));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.DoesNotContain("secret-value", body);
        Assert.DoesNotContain("InvalidOperationException", body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("INTERNAL_SERVER_ERROR", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("test-trace-id", document.RootElement.GetProperty("traceId").GetString());
    }

    private static async Task<(DefaultHttpContext Context, string Body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test";
        context.TraceIdentifier = "test-trace-id";
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            _ => Task.FromException(exception),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return (context, await reader.ReadToEndAsync());
    }
}
