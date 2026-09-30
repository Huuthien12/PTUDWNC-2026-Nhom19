using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeRepository
{
    // Returns a tracked recipe, including its owned Nutrition, for editing.
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default);

    // Use the client's decoded Base64 token, not the token from a fresh database read.
    // Empty tokens are supported for legacy rows; request validation is the caller's responsibility.
    void Update(Recipe recipe, byte[] originalRowVersion);

    Task<bool> SlugExistsAsync(string slug, Guid? excludeRecipeId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountByCategoryAsync(
        Guid categoryId,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Recipe>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int pageSize,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<int> CountPublishedByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<int> CountAllByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<Recipe?> GetBySlugAsync(
        string slug,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
