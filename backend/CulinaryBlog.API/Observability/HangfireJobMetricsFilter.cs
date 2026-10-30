using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hangfire.Server;

namespace CulinaryBlog.API.Observability;

public sealed class HangfireJobMetricsFilter : IServerFilter
{
    public const string MeterName = "CulinaryBlog.Hangfire";
    public const string DurationName = "culinaryblog.hangfire.job.duration";
    public const string ExecutionsName = "culinaryblog.hangfire.job.executions";
    private const string StartedAtKey = "CulinaryBlog.Hangfire.StartedAt";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        DurationName,
        unit: "ms",
        description: "Hangfire job execution attempt duration.");
    private static readonly Counter<long> Executions = Meter.CreateCounter<long>(
        ExecutionsName,
        unit: "{execution}",
        description: "Completed Hangfire job execution attempts.");

    public void OnPerforming(PerformingContext context) =>
        context.Items[StartedAtKey] = Stopwatch.GetTimestamp();

    public void OnPerformed(PerformedContext context)
    {
        if (!context.Items.TryGetValue(StartedAtKey, out var value) || value is not long startedAt)
            return;

        var job = context.BackgroundJob.Job;
        var jobType = $"{job.Type.FullName ?? job.Type.Name}.{job.Method.Name}";
        var outcome = context.Exception is null && !context.Canceled ? "success" : "failure";
        var tags = new TagList
        {
            { "job.type", jobType },
            { "outcome", outcome }
        };

        Duration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, tags);
        Executions.Add(1, tags);
    }
}
