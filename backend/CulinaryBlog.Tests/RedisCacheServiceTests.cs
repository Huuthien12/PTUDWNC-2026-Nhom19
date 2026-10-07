using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RedisCacheServiceTests
{
    [Fact]
    public async Task Existing_call_uses_sliding_expiration()
    {
        var fake = new RecordingDistributedCache();
        ICacheService service = new RedisCacheService(fake);
        var expiration = TimeSpan.FromMinutes(30);
        using var cancellation = new CancellationTokenSource();

        await service.SetAsync("test:sliding", "value", expiration, cancellation.Token);

        Assert.Equal(expiration, fake.Options!.SlidingExpiration);
        Assert.Null(fake.Options.AbsoluteExpirationRelativeToNow);
        Assert.Null(fake.Options.AbsoluteExpiration);
        Assert.Equal("test:sliding", fake.Key);
        Assert.Equal(cancellation.Token, fake.Token);
    }

    [Fact]
    public async Task Absolute_choice_uses_only_relative_absolute_expiration()
    {
        var fake = new RecordingDistributedCache();
        ICacheService service = new RedisCacheService(fake);
        var expiration = TimeSpan.FromMinutes(5);

        await service.SetAsync("test:absolute", "value", expiration,
            useAbsoluteExpiration: true);

        Assert.Equal(expiration, fake.Options!.AbsoluteExpirationRelativeToNow);
        Assert.Null(fake.Options.SlidingExpiration);
        Assert.Null(fake.Options.AbsoluteExpiration);
        Assert.Equal("test:absolute", fake.Key);
    }

    private sealed class RecordingDistributedCache : IDistributedCache
    {
        public DistributedCacheEntryOptions? Options { get; private set; }
        public string? Key { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Key = key;
            Options = options;
            Token = token;
            return Task.CompletedTask;
        }

        // Fail immediately if the service unexpectedly performs any other operation.
        public byte[]? Get(string key) => throw new NotSupportedException();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }
}
