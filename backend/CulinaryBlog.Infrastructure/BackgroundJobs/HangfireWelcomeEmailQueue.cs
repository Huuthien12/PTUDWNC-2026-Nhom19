using System.Runtime.CompilerServices;
using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

[assembly: InternalsVisibleTo("CulinaryBlog.Tests")]

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class HangfireWelcomeEmailQueue : IWelcomeEmailQueue
{
    private readonly IBackgroundJobClient _jobs;
    private readonly IWelcomeEmailDeduplicationStore _deduplication;

    public HangfireWelcomeEmailQueue(IBackgroundJobClient jobs, JobStorage storage)
        : this(jobs, new HangfireWelcomeEmailDeduplicationStore(storage))
    {
    }

    internal HangfireWelcomeEmailQueue(
        IBackgroundJobClient jobs,
        IWelcomeEmailDeduplicationStore deduplication)
    {
        _jobs = jobs;
        _deduplication = deduplication;
    }

    public Task EnqueueAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reservation = _deduplication.Acquire(email);
        if (!reservation.CanEnqueue())
            return Task.CompletedTask;

        _jobs.Create(
            Job.FromExpression<WelcomeEmailJob>(job => job.SendAsync(email, fullName)),
            new EnqueuedState());
        return Task.CompletedTask;
    }
}
