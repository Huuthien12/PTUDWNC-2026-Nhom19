using System.Text.Json;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService>? _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService>? logger = null)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(
            key,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(
            json,
            JsonOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default,
        bool useAbsoluteExpiration = false)
    {
        var json = JsonSerializer.Serialize(
            value,
            JsonOptions);

        var options = new DistributedCacheEntryOptions();
        if (useAbsoluteExpiration)
            options.AbsoluteExpirationRelativeToNow = expiration;
        else
            options.SlidingExpiration = expiration;

        await _cache.SetStringAsync(
            key,
            json,
            options,
            cancellationToken);
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(_logger, key.StartsWith("recipe:") ? "recipe-detail eviction" : "distributed eviction", error); }
    }
}
