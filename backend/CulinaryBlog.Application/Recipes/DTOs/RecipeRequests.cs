using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.DTOs;

public interface IRecipeInput
{
    string Title { get; }
    string Description { get; }
    Guid CategoryId { get; }
    int PrepTime { get; }
    int CookTime { get; }
    int Servings { get; }
    RecipeDifficulty Difficulty { get; }
    RecipeNutritionDto? Nutrition { get; }
}

public sealed record CreateRecipeRequest(
    string Title,
    string Description,
    Guid CategoryId,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    string? Instructions = null,
    RecipeNutritionDto? Nutrition = null) : IRecipeInput;

public sealed record UpdateRecipeRequest(
    string Title,
    string Description,
    Guid CategoryId,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    string Instructions,
    string RowVersion,
    RecipeNutritionDto? Nutrition = null) : IRecipeInput;
