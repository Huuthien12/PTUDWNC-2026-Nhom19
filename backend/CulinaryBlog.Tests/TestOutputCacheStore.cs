using Microsoft.AspNetCore.OutputCaching;

namespace CulinaryBlog.Tests;

public sealed class TestOutputCacheStore : IOutputCacheStore
{
    private readonly Dictionary<string, (byte[] Value, string[] Tags, DateTimeOffset Expires)> entries = [];
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public int Hits { get; private set; }
    public List<TimeSpan> Lifetimes { get; } = [];
    public List<string> EvictedTags { get; } = [];
    public bool FailRead { get; set; }
    public bool FailWrite { get; set; }
    public bool FailEvict { get; set; }
    public Action? OnEvict { get; set; }

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (FailRead) throw new IOException("Simulated cache transport failure");
        lock (entries)
        {
            if (entries.TryGetValue(key, out var entry) && entry.Expires > Now)
            { Hits++; return ValueTask.FromResult<byte[]?>(entry.Value); }
            return ValueTask.FromResult<byte[]?>(null);
        }
    }

    public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (FailWrite) throw new IOException("Simulated cache transport failure");
        lock (entries) { Lifetimes.Add(validFor); entries[key] = (value, tags ?? [], Now + validFor); }
        return ValueTask.CompletedTask;
    }

    public ValueTask EvictByTagAsync(string tag, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        OnEvict?.Invoke();
        EvictedTags.Add(tag);
        if (FailEvict) throw new IOException("Simulated cache transport failure");
        lock (entries)
            foreach (var key in entries.Where(pair => pair.Value.Tags.Contains(tag)).Select(pair => pair.Key).ToArray())
                entries.Remove(key);
        return ValueTask.CompletedTask;
    }
}
