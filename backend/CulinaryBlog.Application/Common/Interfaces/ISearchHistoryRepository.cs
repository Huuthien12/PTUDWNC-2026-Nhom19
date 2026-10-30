using SearchHistoryEntity = CulinaryBlog.Domain.Entities.SearchHistory;

namespace CulinaryBlog.Application.Common.Interfaces;

public interface ISearchHistoryRepository
{
    Task<IReadOnlyList<SearchHistoryEntity>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<SearchHistoryEntity?> GetAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<SearchHistoryEntity?> GetByQueryAsync(string userId, string query, CancellationToken cancellationToken = default);
    Task AddAsync(SearchHistoryEntity history, CancellationToken cancellationToken = default);
    void Remove(SearchHistoryEntity history);
    void RemoveRange(IEnumerable<SearchHistoryEntity> history);
}
