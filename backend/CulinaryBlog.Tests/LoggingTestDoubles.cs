using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Tests;

internal sealed record RecordedLog(
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordedLog> _entries = new();
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, object?>> _scopes = new();

    public IReadOnlyCollection<RecordedLog> Entries => _entries.ToArray();
    public IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Scopes => _scopes.ToArray();
    public string AllText => string.Join(Environment.NewLine, _entries.Select(entry =>
        $"{entry.Message} {string.Join(' ', entry.Properties.Values)}"));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var properties = new Dictionary<string, object?>();
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
                properties[pair.Key] = pair.Value;
        }

        _scopes.Enqueue(properties);
        return NullScope.Instance;
    }
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = new Dictionary<string, object?>();
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
                properties[pair.Key] = pair.Value;
        }

        _entries.Enqueue(new RecordedLog(logLevel, formatter(state, exception), exception, properties));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;
    public override long TimestampFrequency => 1_000;
    public override long GetTimestamp() => _timestamp;
    public void Advance(TimeSpan duration) => _timestamp += (long)duration.TotalMilliseconds;
}
