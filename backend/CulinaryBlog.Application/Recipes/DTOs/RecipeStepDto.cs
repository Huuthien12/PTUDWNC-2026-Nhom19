namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeStepDto(
    Guid Id,
    int StepNumber,
    string? Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl,
    string RowVersion
);

public sealed record RecipeStepListDto(
    IReadOnlyList<RecipeStepDto> Items,
    string RowVersion
);
