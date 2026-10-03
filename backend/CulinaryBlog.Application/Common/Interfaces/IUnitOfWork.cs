namespace CulinaryBlog.Application.Common.Interfaces;

public interface IUnitOfWork
{
    ICategoryRepository Categories { get; }

    IRecipeRepository Recipes { get; }

    ISearchHistoryRepository SearchHistories { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}
