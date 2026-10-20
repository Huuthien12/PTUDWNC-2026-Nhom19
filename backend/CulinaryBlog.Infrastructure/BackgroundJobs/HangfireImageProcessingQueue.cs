using System.Runtime.CompilerServices;
using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

[assembly: InternalsVisibleTo("CulinaryBlog.Tests")]

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class HangfireImageProcessingQueue : IImageProcessingQueue
{
    private readonly IBackgroundJobClient _jobs;
    private readonly IImageProcessingDeduplicationStore _deduplication;

    public HangfireImageProcessingQueue(IBackgroundJobClient jobs, JobStorage storage)
        : this(jobs, new HangfireImageProcessingDeduplicationStore(storage))
    {
    }

    internal HangfireImageProcessingQueue(IBackgroundJobClient jobs,
        IImageProcessingDeduplicationStore deduplication)
    {
        _jobs = jobs;
        _deduplication = deduplication;
    }

    public void EnqueueProcess(Guid recipeId, Guid imageId, string originalObjectKey)
    {
        using var reservation = _deduplication.Acquire(imageId);
        if (!reservation.CanEnqueue()) return;

        _jobs.Create(
            Job.FromExpression<RecipeImageProcessingJob>(job =>
                job.ProcessAsync(recipeId, imageId, originalObjectKey)),
            new EnqueuedState());
    }
}

internal interface IImageProcessingDeduplicationStore
{
    IImageProcessingReservation Acquire(Guid imageId);
}

internal interface IImageProcessingReservation : IDisposable
{
    bool CanEnqueue();
}

internal sealed class HangfireImageProcessingDeduplicationStore(JobStorage storage)
    : IImageProcessingDeduplicationStore
{
    public IImageProcessingReservation Acquire(Guid imageId)
    {
        var connection = storage.GetConnection();
        try
        {
            var key = ImageProcessingJobAssociation.Key(imageId);
            var distributedLock = connection.AcquireDistributedLock(key, TimeSpan.FromSeconds(30));
            return new Reservation(connection, distributedLock, key);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private sealed class Reservation(IStorageConnection connection, IDisposable distributedLock, string key)
        : IImageProcessingReservation
    {
        public bool CanEnqueue()
        {
            var entry = connection.GetAllEntriesFromHash(key);
            if (entry is null) return true;

            JobData? job = null;
            if (entry.TryGetValue(ImageProcessingJobAssociation.JobIdField, out var jobId) &&
                !string.IsNullOrWhiteSpace(jobId))
                job = connection.GetJobData(jobId);

            return ImageProcessingDeduplication.CanEnqueue(job?.State);
        }

        public void Dispose()
        {
            distributedLock.Dispose();
            connection.Dispose();
        }
    }
}

internal static class ImageProcessingDeduplication
{
    public static bool CanEnqueue(string? state) => state is null or "Succeeded" or "Failed" or "Deleted";
}

internal static class ImageProcessingJobAssociation
{
    internal const string JobIdField = "JobId";

    internal static string Key(Guid imageId) => $"image-processing:{imageId:D}";

    internal static void Track(IWriteOnlyTransaction transaction, Guid imageId, string jobId) =>
        transaction.SetRangeInHash(Key(imageId), [new(JobIdField, jobId)]);
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class TrackImageProcessingJobAttribute : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState.Name != EnqueuedState.StateName ||
            context.BackgroundJob.Job.Args.Count < 2 ||
            context.BackgroundJob.Job.Args[1] is not Guid imageId) return;

        // The association is committed with the Enqueued state and queue row.
        ImageProcessingJobAssociation.Track(transaction, imageId, context.BackgroundJob.Id);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
