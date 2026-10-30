namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeNutritionDto(
    decimal? Calories = null,
    decimal? Protein = null,
    decimal? Carbs = null,
    decimal? Fat = null);
