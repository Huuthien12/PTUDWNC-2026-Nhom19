using CulinaryBlog.API.Caching;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class CacheFailurePolicyTests
{
    private sealed class FailingStore(Exception error) : IOutputCacheStore
    {
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken token) => throw error;
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan ttl, CancellationToken token) => throw error;
        public ValueTask EvictByTagAsync(string tag, CancellationToken token) => throw error;
    }
    private sealed class LogRecorder : ILogger<ResilientOutputCacheStore>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> format)
        {
            Assert.Null(exception);
            Messages.Add(format(state, exception));
        }
    }

    [Fact]
    public async Task Infrastructure_failure_degrades_each_output_operation_and_logs_no_provider_secrets()
    {
        var logger = new LogRecorder();
        var store = new ResilientOutputCacheStore(new FailingStore(new IOException("password=never-log-this")), logger);
        Assert.Null(await store.GetAsync("key", default));
        await store.SetAsync("key", [], ["recipes"], TimeSpan.FromMinutes(2), default);
        await store.EvictByTagAsync("recipes", default);
        Assert.Equal(3, logger.Messages.Count);
        Assert.All(logger.Messages, message => Assert.DoesNotContain("never-log-this", message));
        Assert.Contains(logger.Messages, message => message.Contains("output read"));
        Assert.Contains(logger.Messages, message => message.Contains("output write"));
        Assert.Contains(logger.Messages, message => message.Contains("output tag eviction"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_and_programming_errors_are_never_disguised_as_cache_misses(bool canceled)
    {
        Exception error = canceled ? new OperationCanceledException() : new InvalidOperationException();
        var store = new ResilientOutputCacheStore(new FailingStore(error), new LogRecorder());
        var readError = await Record.ExceptionAsync(async () => await store.GetAsync("key", default));
        var writeError = await Record.ExceptionAsync(async () => await store.SetAsync("key", [], [], TimeSpan.FromMinutes(2), default));
        var evictError = await Record.ExceptionAsync(async () => await store.EvictByTagAsync("recipes", default));
        Assert.Same(error, readError); Assert.Same(error, writeError); Assert.Same(error, evictError);
        var cache = new RecordingRecipeCache { OnRemoveAsync = _ => throw error };
        Assert.Same(error, await Record.ExceptionAsync(() => cache.RemoveSafelyAsync("recipe:test", default)));
    }
}
