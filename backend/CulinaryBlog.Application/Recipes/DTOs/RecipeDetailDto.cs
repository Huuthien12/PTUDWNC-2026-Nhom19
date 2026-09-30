using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed class RecipeDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    public int PrepTime { get; set; }
    public int CookTime { get; set; }
    public int Servings { get; set; }

    public RecipeDifficulty Difficulty { get; set; }
    public RecipeStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public RecipeCategoryDto Category { get; set; } = new();
    public AuthorDto Author { get; set; } = new();

    public NutritionDto? Nutrition { get; set; }

    public IReadOnlyList<IngredientDto> Ingredients { get; set; }
        = Array.Empty<IngredientDto>();

    public IReadOnlyList<StepDto> Steps { get; set; }
        = Array.Empty<StepDto>();

    public IReadOnlyList<ImageDto> Images { get; set; }
        = Array.Empty<ImageDto>();
}

public sealed class RecipeCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class AuthorDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public sealed class NutritionDto
{
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fat { get; set; }
}

public sealed class IngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public int OrderIndex { get; set; }
}

public sealed class StepDto
{
    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public string? Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? TimerMinutes { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class ImageDto
{
    public Guid Id { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string? MediumUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? AltText { get; set; }
    public bool IsPrimary { get; set; }
    public int OrderIndex { get; set; }
}