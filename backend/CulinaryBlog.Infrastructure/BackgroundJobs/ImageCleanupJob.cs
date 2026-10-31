using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

// FR-FILE-002: object deletion goes through IFileStorageService (idempotent when the object is gone);
// a connection failure throws and Hangfire retries up to 3 times.
public sealed class ImageCleanupJob(IRecipeImageObjectCleanup objectCleanup, ILogger<ImageCleanupJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!await objectCleanup.DeleteIfUnreferencedAsync(objectKey, cancellationToken))
            logger.LogInformation("Skipped deleting image object {ObjectKey} because it is still referenced.",
                objectKey);
    }
}
