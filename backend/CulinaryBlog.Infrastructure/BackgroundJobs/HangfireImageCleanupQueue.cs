using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class HangfireImageCleanupQueue(IBackgroundJobClient jobs) : IImageCleanupQueue
{
    public void EnqueueDelete(string objectKey)
        => jobs.Enqueue<ImageCleanupJob>(job => job.DeleteAsync(objectKey));
}
