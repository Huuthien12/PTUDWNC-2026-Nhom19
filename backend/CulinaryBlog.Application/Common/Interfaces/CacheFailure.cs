using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Interfaces;

// Only cache operations belong inside these guards. DB/authorization errors must propagate.
public static class CacheFailure
{
    public static bool IsInfrastructure(Exception error) =>
        error is IOException or TimeoutException or SocketException or JsonException ||
        IsRedisException(error.GetType());

    private static bool IsRedisException(Type? type) => type is not null &&
        (type.FullName == "StackExchange.Redis.RedisException" || IsRedisException(type.BaseType));

    public static void Log(ILogger? logger, string operation, Exception error) =>
        // Do not log exception messages: providers may include connection strings or payloads.
        logger?.LogWarning("Cache {Operation} failed ({ErrorType}); continuing without cache", operation, error.GetType().Name);

    public static async Task RemoveSafelyAsync(this ICacheService cache, string key,
        CancellationToken cancellationToken, ILogger? logger = null)
    {
        try { await cache.RemoveAsync(key, cancellationToken); }
        catch (Exception error) when (IsInfrastructure(error)) { Log(logger, "eviction", error); }
    }
}
