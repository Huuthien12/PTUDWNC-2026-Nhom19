using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Recipes.DTOs;
using Npgsql;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class RecipeRepository : IRecipeRepository
{
    private readonly AppDbContext _context;

    public RecipeRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Recipe?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _context.Recipes.FirstOrDefaultAsync(
            recipe => recipe.Id == id,
            cancellationToken);

    public Task<Recipe?> GetForLifecycleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _context.Recipes
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Steps)
            .FirstOrDefaultAsync(
                recipe => recipe.Id == id,
                cancellationToken);

    public Task<Recipe?> GetForStepMutationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _context.Recipes
            .Include(recipe => recipe.Steps
                .OrderBy(step => step.StepNumber))
            .FirstOrDefaultAsync(
                recipe => recipe.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        Recipe recipe,
        CancellationToken cancellationToken = default)
        => await _context.Recipes.AddAsync(
            recipe,
            cancellationToken);

    public void AddIngredient(RecipeIngredient ingredient) => _context.RecipeIngredients.Add(ingredient);

    public async Task AddStepAsync(
        RecipeStep step,
        CancellationToken cancellationToken = default)
        => await _context.RecipeSteps.AddAsync(
            step,
            cancellationToken);

    public void Update(
    Recipe recipe,
    byte[] originalRowVersion)
{
    ArgumentNullException.ThrowIfNull(originalRowVersion);

    var entry = _context.Entry(recipe);

    if (entry.State != EntityState.Unchanged &&
        entry.State != EntityState.Modified)
    {
        throw new InvalidOperationException(
            "Load a tracked Recipe with GetByIdAsync before updating it.");
    }

    // Mark only the root, not its Author, Category, or existing child collections.
    entry.State = EntityState.Modified;

    entry.Property(x => x.RowVersion)
        .OriginalValue = originalRowVersion.ToArray();

    // RecipeStep has an application-generated Guid key.
    // Explicitly mark newly added steps as Added so EF generates
    // INSERT instead of UPDATE.
    foreach (var step in recipe.Steps)
    {
        var stepEntry = _context.Entry(step);

        if (stepEntry.State == EntityState.Detached)
        {
            stepEntry.State = EntityState.Added;
        }
    }
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

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludeRecipeId = null,
        CancellationToken cancellationToken = default)
        => _context.Recipes
            .IgnoreQueryFilters()
            .AnyAsync(
                recipe =>
                    recipe.Slug == slug &&
                    recipe.Id != excludeRecipeId,
                cancellationToken);

    // ============================================================
    // GET /api/v1/recipes
    // Visibility + filters
    // ============================================================

    private IQueryable<Recipe> BuildVisibleQuery(
        string? userId,
        bool isAdmin,
        Guid? categoryId,
        RecipeDifficulty? difficulty,
        int? maxCookTime,
        int? minServings)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Where(recipe => !recipe.IsDeleted);

        // Visibility
        if (!isAdmin)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(recipe =>
                    recipe.Status == RecipeStatus.Published ||
                    ((recipe.Status == RecipeStatus.Draft ||
                      recipe.Status == RecipeStatus.Archived) &&
                     recipe.AuthorId == userId));
            }
            else
            {
                query = query.Where(recipe =>
                    recipe.Status == RecipeStatus.Published);
            }
        }

        // Filter: categoryId
        if (categoryId.HasValue)
        {
            query = query.Where(recipe =>
                recipe.CategoryId == categoryId.Value);
        }

        // Filter: difficulty
        if (difficulty.HasValue)
        {
            query = query.Where(recipe =>
                recipe.Difficulty == difficulty.Value);
        }

        // Filter: maxCookTime
        if (maxCookTime.HasValue)
        {
            query = query.Where(recipe =>
                recipe.CookTime <= maxCookTime.Value);
        }

        // Filter: minServings
        if (minServings.HasValue)
        {
            query = query.Where(recipe =>
                recipe.Servings >= minServings.Value);
        }

        return query;
    }

    public Task<int> CountVisibleAsync(
        string? userId,
        bool isAdmin,
        Guid? categoryId,
        RecipeDifficulty? difficulty,
        int? maxCookTime,
        int? minServings,
        CancellationToken cancellationToken = default)
    {
        return BuildVisibleQuery(
                userId,
                isAdmin,
                categoryId,
                difficulty,
                maxCookTime,
                minServings)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetVisibleAsync(
        int page,
        int pageSize,
        string? userId,
        bool isAdmin,
        Guid? categoryId,
        RecipeDifficulty? difficulty,
        int? maxCookTime,
        int? minServings,
        string sortBy,
        string sortOrder,
        CancellationToken cancellationToken = default)
    {
        var query = BuildVisibleQuery(
            userId,
            isAdmin,
            categoryId,
            difficulty,
            maxCookTime,
            minServings);

        var descending = sortOrder.Equals(
            "desc",
            StringComparison.OrdinalIgnoreCase);

        query = sortBy.ToLowerInvariant() switch
        {
            "title" => descending
                ? query
                    .OrderByDescending(recipe => recipe.Title)
                    .ThenBy(recipe => recipe.Id)
                : query
                    .OrderBy(recipe => recipe.Title)
                    .ThenBy(recipe => recipe.Id),

            "createdat" => descending
                ? query
                    .OrderByDescending(recipe => recipe.CreatedAt)
                    .ThenBy(recipe => recipe.Id)
                : query
                    .OrderBy(recipe => recipe.CreatedAt)
                    .ThenBy(recipe => recipe.Id),

            _ => descending
                ? query
                    .OrderByDescending(recipe => recipe.CreatedAt)
                    .ThenBy(recipe => recipe.Id)
                : query
                    .OrderBy(recipe => recipe.CreatedAt)
                    .ThenBy(recipe => recipe.Id)
        };

        return await query
            .Include(recipe => recipe.Images)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    // ============================================================
    // Category queries
    // ============================================================

    private IQueryable<Recipe> BuildCategoryQuery(
        Guid categoryId,
        string? userId,
        bool isAdmin)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Where(recipe =>
                recipe.CategoryId == categoryId &&
                !recipe.IsDeleted);

        if (isAdmin)
        {
            return query;
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return query.Where(recipe =>
                recipe.Status == RecipeStatus.Published ||
                (recipe.Status == RecipeStatus.Draft &&
                 recipe.AuthorId == userId));
        }

        return query.Where(recipe =>
            recipe.Status == RecipeStatus.Published);
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
            .Include(recipe => recipe.Images)
            .OrderByDescending(recipe => recipe.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    // ============================================================
    // Recipe detail by slug
    // ============================================================

    public async Task<Recipe?> GetBySlugAsync(
        string slug,
        string? userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Where(recipe =>
                recipe.Slug == slug &&
                !recipe.IsDeleted);

        if (!isAdmin)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(recipe =>
                    recipe.Status == RecipeStatus.Published ||
                    ((recipe.Status == RecipeStatus.Draft ||
                      recipe.Status == RecipeStatus.Archived) &&
                     recipe.AuthorId == userId));
            }
            else
            {
                query = query.Where(recipe =>
                    recipe.Status == RecipeStatus.Published);
            }
        }

        return await query
            .Include(recipe => recipe.Category)
            .Include(recipe => recipe.Author)
            .Include(recipe => recipe.Nutrition)
            .Include(recipe => recipe.Ingredients
                .Where(ingredient => !ingredient.IsDeleted)
                .OrderBy(ingredient => ingredient.OrderIndex))
            .Include(recipe => recipe.Steps
                .Where(step => !step.IsDeleted)
                .OrderBy(step => step.StepNumber))
            .Include(recipe => recipe.Images
                .Where(image => !image.IsDeleted)
                .OrderBy(image => image.OrderIndex))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ============================================================
    // Category statistics
    // ============================================================

    public async Task<IReadOnlyList<RecipeSuggestionDto>> GetSuggestionsAsync(
    string normalizedQuery,
    CancellationToken cancellationToken = default)
{
    var recipeSuggestions = await _context.Recipes
        .AsNoTracking()
        .Where(recipe =>
            !recipe.IsDeleted &&
            recipe.Status == RecipeStatus.Published &&
            recipe.Title.ToLower().StartsWith(normalizedQuery))
        .Select(recipe => new RecipeSuggestionDto(
            recipe.Title,
            "recipe",
            recipe.Slug))
        .Take(10)
        .ToListAsync(cancellationToken);

    var categorySuggestions = await _context.Categories
        .AsNoTracking()
        .Where(category =>
            !category.IsDeleted &&
            category.Name.ToLower().StartsWith(normalizedQuery))
        .Select(category => new RecipeSuggestionDto(
            category.Name,
            "category",
            category.Slug))
        .Take(10)
        .ToListAsync(cancellationToken);

    return recipeSuggestions
        .Concat(categorySuggestions)
        .GroupBy(
            suggestion => $"{suggestion.Type}:{suggestion.Text}",
            StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(suggestion =>
            suggestion.Text.Equals(
                normalizedQuery,
                StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1)
        .ThenBy(suggestion => suggestion.Text)
        .Take(10)
        .ToList();
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
