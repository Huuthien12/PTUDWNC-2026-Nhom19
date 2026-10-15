using System.Diagnostics;
using System.Security.Claims;

namespace CulinaryBlog.API.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = timeProvider.GetTimestamp();
        await next(context);

        var elapsed = timeProvider.GetElapsedTime(startedAt);
        var level = GetLogLevel(context, elapsed);
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is { Length: > 64 })
            userId = null;

        logger.Log(
            level,
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms " +
            "with {CorrelationId}, {TraceId}, {UserId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds,
            correlationId,
            traceId,
            userId);
    }

    private static LogLevel GetLogLevel(HttpContext context, TimeSpan elapsed)
    {
        if (context.Items.ContainsKey(GlobalExceptionMiddleware.HandledExceptionItemKey))
            return LogLevel.Information;
        if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            return LogLevel.Error;
        if (elapsed > SlowThreshold)
            return LogLevel.Warning;
        if (context.Request.Path.StartsWithSegments("/health"))
            return LogLevel.Debug;
        return LogLevel.Information;
    }
}
