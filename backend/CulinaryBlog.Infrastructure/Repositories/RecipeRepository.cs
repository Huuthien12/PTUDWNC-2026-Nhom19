using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class RecipeRepository : IRecipeRepository
{
    private readonly AppDbContext _context;

    public RecipeRepository(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<Recipe> BuildCategoryQuery(
        Guid categoryId,
        string? userId,
        bool isAdmin)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Where(r =>
                r.CategoryId == categoryId &&
                !r.IsDeleted);

        if (isAdmin)
        {
            return query;
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return query.Where(r =>
                r.Status == RecipeStatus.Published ||
                (r.Status == RecipeStatus.Draft &&
                 r.AuthorId == userId));
        }

        return query.Where(r =>
            r.Status == RecipeStatus.Published);
    }

    public Task<int> CountByCategoryAsync(
        Guid categoryId,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        return BuildCategoryQuery(
                categoryId,
                userId,
                isAdmin)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetByCategoryAsync(
        Guid categoryId,
        int page,
        int pageSize,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        return await BuildCategoryQuery(
                categoryId,
                userId,
                isAdmin)
            .Include(r => r.Images)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
    public async Task<Recipe?> GetBySlugAsync(
        string slug,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
    var query = _context.Recipes
        .AsNoTracking()
        .Where(r =>
            r.Slug == slug &&
            !r.IsDeleted);

    if (!isAdmin)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(r =>
                r.Status == RecipeStatus.Published ||
                (r.Status == RecipeStatus.Draft &&
                 r.AuthorId == userId));
        }
        else
        {
            query = query.Where(r =>
                r.Status == RecipeStatus.Published);
        }
    }

    return await query
        .Include(r => r.Category)
        .Include(r => r.Author)
        .Include(r => r.Nutrition)
        .Include(r => r.Ingredients
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.OrderIndex))
        .Include(r => r.Steps
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.StepNumber))
        .Include(r => r.Images
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.OrderIndex))
        .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountPublishedByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return _context.Recipes
            .AsNoTracking()
            .CountAsync(
                recipe =>
                    recipe.CategoryId == categoryId &&
                    !recipe.IsDeleted &&
                    recipe.Status == RecipeStatus.Published,
                cancellationToken);
    }

    public Task<int> CountAllByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return _context.Recipes
            .AsNoTracking()
            .CountAsync(
                recipe =>
                    recipe.CategoryId == categoryId,
                cancellationToken);
    }
}