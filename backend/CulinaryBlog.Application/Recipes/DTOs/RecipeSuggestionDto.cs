namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeSuggestionDto(
    string Text,
    string Type,
    string? Slug
);

public sealed record RecipeSuggestionsDto(
    IReadOnlyList<RecipeSuggestionDto> Items
);
