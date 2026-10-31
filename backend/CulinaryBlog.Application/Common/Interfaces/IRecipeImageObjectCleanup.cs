namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeImageObjectCleanup
{
    Task<bool> DeleteIfUnreferencedAsync(string objectKey, CancellationToken cancellationToken = default);
}
