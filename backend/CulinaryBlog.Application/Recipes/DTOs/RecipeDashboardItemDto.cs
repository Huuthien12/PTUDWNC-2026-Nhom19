using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeDashboardItemDto(
    Guid Id,
    string Title,
    string Slug,
    RecipeStatus Status,
    string RowVersion,
    string CategoryName,
    string? ThumbnailUrl,
    string? MediumUrl,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
