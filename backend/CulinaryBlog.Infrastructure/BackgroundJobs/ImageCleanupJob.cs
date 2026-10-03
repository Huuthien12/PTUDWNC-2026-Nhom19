using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

// FR-FILE-002: object deletion goes through IFileStorageService (idempotent when the object is gone);
// a connection failure throws and Hangfire retries up to 3 times.
public sealed class ImageCleanupJob(IFileStorageService storage)
{
    [AutomaticRetry(Attempts = 3)]
    public Task DeleteAsync(string objectKey) => storage.DeleteAsync(objectKey);
}
