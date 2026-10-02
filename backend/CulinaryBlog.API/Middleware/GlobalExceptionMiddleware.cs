using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace CulinaryBlog.API.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "Unhandled request error for {RequestPath}", context.Request.Path);

            var problem = CreateProblemDetails(context, exception);
            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                cancellationToken: context.RequestAborted);
        }
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var (status, code, title, detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Validation failed.", "One or more validation errors occurred."),
            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.ErrorCode, "Resource not found.", notFound.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.ErrorCode, "Resource conflict.", conflict.Message),
            BusinessRuleException business => (StatusCodes.Status400BadRequest, business.ErrorCode, "Business rule violation.", business.Message),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, forbidden.ErrorCode, "Forbidden.", forbidden.Message),
            ConcurrencyException concurrency => (StatusCodes.Status422UnprocessableEntity, concurrency.ErrorCode, "Optimistic concurrency conflict.", concurrency.Message),
            ServiceUnavailableException unavailable => (StatusCodes.Status503ServiceUnavailable, unavailable.ErrorCode, "Service unavailable.", unavailable.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status422UnprocessableEntity, "RECIPE_CONCURRENCY_CONFLICT", "Optimistic concurrency conflict.", "The resource was updated by another request. Reload and try again."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Recipes_Slug" } } => (StatusCodes.Status409Conflict, "RECIPE_SLUG_EXISTS", "Resource conflict.", "Recipe slug already exists."),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Unauthorized.", "Authentication is required."),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected error occurred.", "An unexpected error occurred.")
        };

        var problem = new ProblemDetails
        {
            Type = code,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        return problem;
    }
}
