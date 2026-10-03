using CulinaryBlog.Application.Common.Interfaces;
using SearchHistoryEntity = CulinaryBlog.Domain.Entities.SearchHistory;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class SearchHistoryRepository(AppDbContext context) : ISearchHistoryRepository
{
    public async Task<IReadOnlyList<SearchHistoryEntity>> GetForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        await context.SearchHistories.Where(x => x.UserId == userId).OrderByDescending(x => x.SearchedAt).ToListAsync(cancellationToken);

    public Task<SearchHistoryEntity?> GetAsync(Guid id, string userId, CancellationToken cancellationToken = default) =>
        context.SearchHistories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public Task<SearchHistoryEntity?> GetByQueryAsync(string userId, string query, CancellationToken cancellationToken = default) =>
        context.SearchHistories.FirstOrDefaultAsync(x => x.UserId == userId && x.Query == query, cancellationToken);

    public async Task AddAsync(SearchHistoryEntity history, CancellationToken cancellationToken = default) =>
        await context.SearchHistories.AddAsync(history, cancellationToken);

    public void Remove(SearchHistoryEntity history) => context.SearchHistories.Remove(history);
    public void RemoveRange(IEnumerable<SearchHistoryEntity> history) => context.SearchHistories.RemoveRange(history);
}
