using CulinaryBlog.Application.Recipes.DTOs;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Validators;

public abstract class RecipeIngredientRequestValidator<T> : AbstractValidator<T>
    where T : class
{
    protected RecipeIngredientRequestValidator()
    {
        RuleFor(x => x).Must(request => request switch
        {
            CreateRecipeIngredientRequest create => !string.IsNullOrWhiteSpace(create.Name),
            UpdateRecipeIngredientRequest update => !string.IsNullOrWhiteSpace(update.Name),
            _ => false
        }).WithMessage("Name is required.");
    }
}

public sealed class CreateRecipeIngredientRequestValidator
    : AbstractValidator<CreateRecipeIngredientRequest>
{
    public CreateRecipeIngredientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(1, 100);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowVersion).Must(IsCanonicalBase64)
            .WithMessage("RowVersion must be a non-empty canonical Base64 string.");
    }

    internal static bool IsCanonicalBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length > 0 && Convert.ToBase64String(bytes) == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class UpdateRecipeIngredientRequestValidator
    : AbstractValidator<UpdateRecipeIngredientRequest>
{
    public UpdateRecipeIngredientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(1, 100);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowVersion).Must(CreateRecipeIngredientRequestValidator.IsCanonicalBase64)
            .WithMessage("RowVersion must be a non-empty canonical Base64 string.");
    }
}
