namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeStepDto(
    Guid Id,
    int StepNumber,
    string Description,
    int? DurationMinutes,
    string? ImageUrl
);

public sealed record RecipeStepListDto(
    IReadOnlyList<RecipeStepDto> Items,
    string RowVersion
);