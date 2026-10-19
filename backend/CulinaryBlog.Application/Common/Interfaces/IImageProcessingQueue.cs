namespace CulinaryBlog.Application.Common.Interfaces;

public interface IImageProcessingQueue
{
    void EnqueueProcess(Guid recipeId, Guid imageId, string originalObjectKey);
}
