namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record CreateRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string Unit,
    string? Notes = null,
    int SortOrder = 0,
    string RowVersion = "");

public sealed record UpdateRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string Unit,
    string? Notes,
    int SortOrder,
    string RowVersion);

public sealed record RecipeIngredientDto(
    Guid Id,
    string Name,
    decimal? Quantity,
    string Unit,
    string? Notes,
    int SortOrder,
    string RowVersion);
