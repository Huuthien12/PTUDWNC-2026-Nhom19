namespace CulinaryBlog.API.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context.Request);
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   [ItemKey] = correlationId
               }))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpRequest request)
    {
        var values = request.Headers[HeaderName];
        if (values.Count == 1 && IsValid(values[0]))
            return values[0]!;

        return Guid.NewGuid().ToString("N");
    }

    internal static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaxLength } && value.All(IsSafeCharacter);

    private static bool IsSafeCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.';
}
