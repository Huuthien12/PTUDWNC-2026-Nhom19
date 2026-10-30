using System.Diagnostics.Metrics;
using System.Reflection;
using CulinaryBlog.API.Observability;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class HangfireJobMetricsFilterTests
{
    [Fact]
    public void Successful_attempt_emits_duration_and_success_completion()
    {
        using var metrics = new MetricRecorder();
        var (filter, performing, performed) = CreateContexts("safe-token", null);

        filter.OnPerforming(performing);
        filter.OnPerformed(performed);

        var duration = Assert.Single(metrics.Measurements,
            item => item.Instrument == HangfireJobMetricsFilter.DurationName);
        var execution = Assert.Single(metrics.Measurements,
            item => item.Instrument == HangfireJobMetricsFilter.ExecutionsName);
        Assert.True(duration.Value >= 0);
        Assert.Equal("ms", duration.Unit);
        Assert.Equal(1, execution.Value);
        Assert.Equal("{execution}", execution.Unit);
        Assert.Equal(["job.type", "outcome"], execution.Tags.Keys.Order());
        Assert.Equal("success", execution.Tags["outcome"]);
        Assert.Equal(ExpectedJobType, execution.Tags["job.type"]);
    }

    [Fact]
    public void Failed_attempt_emits_duration_and_failure_without_handling_exception()
    {
        using var metrics = new MetricRecorder();
        var exception = new InvalidOperationException("SECRET-EXCEPTION-MESSAGE");
        var (filter, performing, performed) = CreateContexts("safe-token", exception);

        filter.OnPerforming(performing);
        filter.OnPerformed(performed);

        Assert.Single(metrics.Measurements,
            item => item.Instrument == HangfireJobMetricsFilter.DurationName);
        var execution = Assert.Single(metrics.Measurements,
            item => item.Instrument == HangfireJobMetricsFilter.ExecutionsName);
        Assert.Equal("failure", execution.Tags["outcome"]);
        Assert.Same(exception, performed.Exception);
        Assert.False(performed.ExceptionHandled);
    }

    [Fact]
    public void Arguments_job_id_and_exception_do_not_affect_tags_or_cardinality()
    {
        const string first = "SECRET-HANGFIRE-RESET-TOKEN-DO-NOT-EXPORT";
        const string second = "secret-user@example.invalid";
        using var metrics = new MetricRecorder();

        foreach (var value in new[] { first, second })
        {
            var (filter, performing, performed) = CreateContexts(
                value,
                new InvalidOperationException("SECRET-EXCEPTION-MESSAGE"));
            filter.OnPerforming(performing);
            filter.OnPerformed(performed);
        }

        var executions = metrics.Measurements
            .Where(item => item.Instrument == HangfireJobMetricsFilter.ExecutionsName)
            .ToArray();
        Assert.Equal(2, executions.Length);
        Assert.All(executions, execution => Assert.Equal(ExpectedJobType, execution.Tags["job.type"]));
        var exported = string.Join('\n', metrics.Measurements.SelectMany(item =>
            new[] { item.Instrument, item.Description, item.Unit }
                .Concat(item.Tags.Select(tag => $"{tag.Key}={tag.Value}"))));
        Assert.DoesNotContain(first, exported, StringComparison.Ordinal);
        Assert.DoesNotContain(second, exported, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-job-id", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-EXCEPTION-MESSAGE", exported, StringComparison.Ordinal);
    }

    private static string ExpectedJobType =>
        $"{typeof(FakeJob).FullName}.{nameof(FakeJob.Execute)}";

    private static (HangfireJobMetricsFilter Filter, PerformingContext Performing, PerformedContext Performed)
        CreateContexts(string argument, Exception? exception)
    {
        var method = typeof(FakeJob).GetMethod(nameof(FakeJob.Execute))!;
        var job = new Job(typeof(FakeJob), method, new object[] { argument });
        var backgroundJob = new BackgroundJob("sensitive-job-id", job, DateTime.UtcNow);
        var connection = DispatchProxy.Create<IStorageConnection, DefaultProxy>();
        var performContext = new PerformContext(
            new TestJobStorage(connection),
            connection,
            backgroundJob,
            new TestCancellationToken());
        return (
            new HangfireJobMetricsFilter(),
            new PerformingContext(performContext),
            new PerformedContext(performContext, null, false, exception));
    }

    private sealed class FakeJob
    {
        public void Execute(string value) { }
    }

    private sealed record Measurement(
        string Instrument,
        string? Description,
        string? Unit,
        double Value,
        Dictionary<string, object?> Tags);

    private sealed class MetricRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<Measurement> Measurements { get; } = [];

        public MetricRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == HangfireJobMetricsFilter.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Record(instrument, value, tags));
            _listener.Start();
        }

        private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct, IConvertible => Measurements.Add(new Measurement(
                instrument.Name,
                instrument.Description,
                instrument.Unit,
                Convert.ToDouble(value),
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));

        public void Dispose() => _listener.Dispose();
    }

    private class DefaultProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
    }

    private sealed class TestCancellationToken : IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;
        public void ThrowIfCancellationRequested() { }
    }

    private sealed class TestJobStorage(IStorageConnection connection) : JobStorage
    {
        public override IStorageConnection GetConnection() => connection;
        public override IMonitoringApi GetMonitoringApi() =>
            DispatchProxy.Create<IMonitoringApi, DefaultProxy>();
    }
}
