using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Application.Recipes.DTOs;

namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeRepository
{
    Task<int> CountVisibleAsync(
        string? userId,
        bool isAdmin,
        Guid? categoryId,
        RecipeDifficulty? difficulty,
        int? maxCookTime,
        int? minServings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Recipe>> GetVisibleAsync(
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
        CancellationToken cancellationToken = default);

    Task<Recipe?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Recipe?> GetForLifecycleAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Recipe recipe,
        CancellationToken cancellationToken = default);

    Task AddStepAsync(
        RecipeStep step,
        CancellationToken cancellationToken = default);

    void AddIngredient(RecipeIngredient ingredient);

    // Use the client's decoded Base64 token, not the token from a fresh database read.
    // Empty tokens are supported for legacy rows; request validation is the caller's responsibility.
    void Update(Recipe recipe, byte[] originalRowVersion);

    void UpdateForChildMutation(Recipe recipe, byte[] originalRowVersion);

    Task<int> CountSearchAsync(string tsQuery, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchRecipeResult>> SearchAsync(
        string tsQuery, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludeRecipeId = null,
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

    Task<Recipe?> GetForStepMutationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecipeSuggestionDto>> GetSuggestionsAsync(
        string normalizedQuery,
        CancellationToken cancellationToken = default);
}
