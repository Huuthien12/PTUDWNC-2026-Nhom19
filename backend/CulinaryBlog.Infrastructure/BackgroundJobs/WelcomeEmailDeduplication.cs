using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

internal interface IWelcomeEmailDeduplicationStore
{
    IWelcomeEmailReservation Acquire(string email);
}

internal interface IWelcomeEmailReservation : IDisposable
{
    bool CanEnqueue();
}

internal sealed class HangfireWelcomeEmailDeduplicationStore(JobStorage storage)
    : IWelcomeEmailDeduplicationStore
{
    public IWelcomeEmailReservation Acquire(string email)
    {
        var connection = storage.GetConnection();
        var key = WelcomeEmailJobAssociation.Key(email);
        try
        {
            var distributedLock = connection.AcquireDistributedLock(key, TimeSpan.FromSeconds(30));
            return new Reservation(connection, distributedLock, key);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private sealed class Reservation(
        IStorageConnection connection,
        IDisposable distributedLock,
        string key) : IWelcomeEmailReservation
    {
        public bool CanEnqueue()
        {
            var entry = connection.GetAllEntriesFromHash(key);
            if (entry is null ||
                !entry.TryGetValue(WelcomeEmailJobAssociation.JobIdField, out var jobId) ||
                string.IsNullOrWhiteSpace(jobId))
                return true;

            return WelcomeEmailDeduplication.CanEnqueue(connection.GetJobData(jobId)?.State);
        }

        public void Dispose()
        {
            distributedLock.Dispose();
            connection.Dispose();
        }
    }
}

internal static class WelcomeEmailDeduplication
{
    public static bool CanEnqueue(string? state) =>
        state is "Failed" or "Deleted";
}

internal static class WelcomeEmailJobAssociation
{
    internal const string JobIdField = "JobId";

    internal static string Key(string email) =>
        $"welcome-email:{email.Trim().ToUpperInvariant()}";

    internal static void Track(IWriteOnlyTransaction transaction, string email, string jobId) =>
        transaction.SetRangeInHash(Key(email), [new(JobIdField, jobId)]);
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class TrackWelcomeEmailJobAttribute : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState.Name != EnqueuedState.StateName ||
            context.BackgroundJob.Job.Args.Count == 0 ||
            context.BackgroundJob.Job.Args[0] is not string email)
            return;

        WelcomeEmailJobAssociation.Track(transaction, email, context.BackgroundJob.Id);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
