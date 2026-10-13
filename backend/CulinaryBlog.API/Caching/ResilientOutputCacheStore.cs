using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.AspNetCore.OutputCaching;

namespace CulinaryBlog.API.Caching;

// Guards only storage calls, never endpoint execution. Cancellation still propagates.
public sealed class ResilientOutputCacheStore(IOutputCacheStore inner,
    ILogger<ResilientOutputCacheStore> logger) : IOutputCacheStore, IDisposable
{
    public void Dispose() { if (inner is IDisposable disposable) disposable.Dispose(); }
    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try { return await inner.GetAsync(key, cancellationToken); }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "output read", error); return null; }
    }

    public async ValueTask SetAsync(string key, byte[] value, string[]? tags,
        TimeSpan validFor, CancellationToken cancellationToken)
    {
        try { await inner.SetAsync(key, value, tags, validFor, cancellationToken); }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "output write", error); }
    }

    public async ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        try { await inner.EvictByTagAsync(tag, cancellationToken); }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "output tag eviction", error); }
    }
}
