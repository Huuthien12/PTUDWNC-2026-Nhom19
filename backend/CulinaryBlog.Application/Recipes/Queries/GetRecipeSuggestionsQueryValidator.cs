using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed class GetRecipeSuggestionsQueryValidator
    : AbstractValidator<GetRecipeSuggestionsQuery>
{
    public GetRecipeSuggestionsQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .Must(query =>
            {
                var normalized = query?.Trim() ?? string.Empty;
                return normalized.Length >= 2 &&
                       normalized.Length <= 100;
            })
            .WithMessage("Query must contain between 2 and 100 characters.");
    }
}
