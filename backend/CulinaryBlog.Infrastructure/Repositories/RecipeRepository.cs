using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class RecipeRepository : IRecipeRepository
{
    private readonly AppDbContext _context;

    public RecipeRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Recipes.FirstOrDefaultAsync(recipe => recipe.Id == id, cancellationToken);

    public Task<Recipe?> GetForLifecycleAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Recipes.Include(recipe => recipe.Ingredients).Include(recipe => recipe.Steps)
            .FirstOrDefaultAsync(recipe => recipe.Id == id, cancellationToken);

    public async Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default)
        => await _context.Recipes.AddAsync(recipe, cancellationToken);

    public void AddIngredient(RecipeIngredient ingredient) => _context.RecipeIngredients.Add(ingredient);

    public void Update(Recipe recipe, byte[] originalRowVersion)
    {
        ArgumentNullException.ThrowIfNull(originalRowVersion);
        var entry = _context.Entry(recipe);
        if (entry.State != EntityState.Unchanged && entry.State != EntityState.Modified)
            throw new InvalidOperationException("Load a tracked Recipe with GetByIdAsync before updating it.");

        // Mark only the root, not its Author, Category, or child collections.
        entry.State = EntityState.Modified;
        entry.Property(x => x.RowVersion).OriginalValue = originalRowVersion.ToArray();
    }

    public void UpdateForChildMutation(Recipe recipe, byte[] originalRowVersion)
    {
        var entry = _context.Entry(recipe);
        entry.Property(x => x.RowVersion).OriginalValue = originalRowVersion.ToArray();
    }

    public Task<int> CountSearchAsync(string tsQuery, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT count(*)::int AS "Value"
            FROM "Recipes"
            WHERE "IsDeleted" = false
              AND "Status" = 2
              AND "SearchVector" @@ to_tsquery('simple', unaccent(@query))
            """;
        return _context.Database.SqlQueryRaw<int>(
            sql, new NpgsqlParameter("query", tsQuery)).SingleAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SearchRecipeResult>> SearchAsync(
        string tsQuery, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", ts_rank("SearchVector", to_tsquery('simple', unaccent(@query))) AS "Score"
            FROM "Recipes"
            WHERE "IsDeleted" = false
              AND "Status" = 2
              AND "SearchVector" @@ to_tsquery('simple', unaccent(@query))
            ORDER BY "Score" DESC, "CreatedAt" DESC, "Id" ASC
            OFFSET @offset LIMIT @limit
            """;
        var hits = await _context.Database.SqlQueryRaw<SearchHit>(
            sql,
            new NpgsqlParameter("query", tsQuery),
            new NpgsqlParameter("offset", (page - 1) * pageSize),
            new NpgsqlParameter("limit", pageSize)).ToListAsync(cancellationToken);
        if (hits.Count == 0) return [];

        var ids = hits.Select(hit => hit.Id).ToArray();
        var recipes = await _context.Recipes.AsNoTracking()
            .Include(recipe => recipe.Images)
            .Where(recipe => ids.Contains(recipe.Id))
            .ToDictionaryAsync(recipe => recipe.Id, cancellationToken);
        return hits.Where(hit => recipes.ContainsKey(hit.Id))
            .Select(hit => new SearchRecipeResult(recipes[hit.Id], hit.Score))
            .ToList();
    }

    private sealed class SearchHit
    {
        public Guid Id { get; set; }
        public double Score { get; set; }
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? excludeRecipeId = null,
        CancellationToken cancellationToken = default)
        => _context.Recipes.IgnoreQueryFilters().AnyAsync(
            recipe => recipe.Slug == slug && recipe.Id != excludeRecipeId, cancellationToken);

    private IQueryable<Recipe> BuildVisibleQuery(string? userId, bool isAdmin)
    {
        var query = _context.Recipes.AsNoTracking().Where(recipe => !recipe.IsDeleted);
        if (isAdmin) return query;
        return !string.IsNullOrWhiteSpace(userId)
            ? query.Where(recipe => recipe.Status == RecipeStatus.Published ||
                ((recipe.Status == RecipeStatus.Draft || recipe.Status == RecipeStatus.Archived) && recipe.AuthorId == userId))
            : query.Where(recipe => recipe.Status == RecipeStatus.Published);
    }

    public Task<int> CountVisibleAsync(string? userId, bool isAdmin,
        CancellationToken cancellationToken = default) => BuildVisibleQuery(userId, isAdmin).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Recipe>> GetVisibleAsync(int page, int pageSize, string? userId,
        bool isAdmin, CancellationToken cancellationToken = default) => await BuildVisibleQuery(userId, isAdmin)
            .Include(recipe => recipe.Images).OrderByDescending(recipe => recipe.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

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
                ((r.Status == RecipeStatus.Draft || r.Status == RecipeStatus.Archived) &&
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
