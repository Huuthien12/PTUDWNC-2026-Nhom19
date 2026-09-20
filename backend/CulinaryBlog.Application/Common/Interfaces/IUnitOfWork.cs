namespace CulinaryBlog.Application.Common.Interfaces;

public interface IUnitOfWork
{
    ICategoryRepository Categories { get; }

    IRecipeRepository Recipes { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}