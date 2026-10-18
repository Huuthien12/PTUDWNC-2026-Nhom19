using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class HangfireImageProcessingQueue(IBackgroundJobClient jobs) : IImageProcessingQueue
{
    public void EnqueueProcess(Guid recipeId, Guid imageId, string originalObjectKey)
        => jobs.Enqueue<RecipeImageProcessingJob>(
            job => job.ProcessAsync(recipeId, imageId, originalObjectKey));
}
