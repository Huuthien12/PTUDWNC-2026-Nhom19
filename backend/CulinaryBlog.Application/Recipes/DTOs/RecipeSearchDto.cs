using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeSearchDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    string? ThumbnailUrl,
    DateTime? PublishedAt,
    double RelevanceScore);
