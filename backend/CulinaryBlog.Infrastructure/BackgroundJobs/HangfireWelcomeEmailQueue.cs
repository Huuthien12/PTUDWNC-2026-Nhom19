using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class HangfireWelcomeEmailQueue(IBackgroundJobClient jobs) : IWelcomeEmailQueue
{
    public Task EnqueueAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        jobs.Enqueue<WelcomeEmailJob>(job => job.SendAsync(email, fullName));
        return Task.CompletedTask;
    }
}
