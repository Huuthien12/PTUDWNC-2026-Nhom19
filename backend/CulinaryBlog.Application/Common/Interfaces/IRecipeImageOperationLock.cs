namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeImageOperationLock
{
    // Every storage key under a recipe uses the same recipe-scoped lock convention.
    Task<IAsyncDisposable> AcquireAsync(Guid recipeId, CancellationToken cancellationToken = default);
}
