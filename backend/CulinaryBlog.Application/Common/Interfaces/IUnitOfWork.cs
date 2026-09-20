namespace CulinaryBlog.Application.Common.Interfaces;

public interface IUnitOfWork
{
    ICategoryRepository Categories { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}