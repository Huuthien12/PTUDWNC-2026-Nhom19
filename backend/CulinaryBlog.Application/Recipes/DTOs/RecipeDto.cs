using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.DTOs;

// Mutation response; aggregate detail/child DTOs belong to the detail feature.
public sealed record RecipeDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    string Instructions,
    Guid CategoryId,
    string AuthorId,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    RecipeStatus Status,
    RecipeNutritionDto Nutrition,
    string RowVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt);
