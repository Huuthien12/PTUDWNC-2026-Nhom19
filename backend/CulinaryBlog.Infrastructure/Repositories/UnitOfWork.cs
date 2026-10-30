using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context, ICategoryRepository categories, IRecipeRepository recipes)
        : this(context, categories, recipes, new SearchHistoryRepository(context))
    {
    }

    public UnitOfWork(
        AppDbContext context,
        ICategoryRepository categories,
        IRecipeRepository recipes,
        ISearchHistoryRepository searchHistories)
    {
        _context = context;
        Categories = categories;
        Recipes = recipes;
        SearchHistories = searchHistories;
    }

    public ICategoryRepository Categories { get; }

    public IRecipeRepository Recipes { get; }

    public ISearchHistoryRepository SearchHistories { get; }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            await action(cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }
}
