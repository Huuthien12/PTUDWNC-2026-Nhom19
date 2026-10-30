using CulinaryBlog.Application.Recipes.DTOs;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Validators;

public sealed class RecipeNutritionValidator : AbstractValidator<RecipeNutritionDto>
{
    public RecipeNutritionValidator()
    {
        RuleFor(x => x.Calories).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Protein).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Carbs).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Fat).GreaterThanOrEqualTo(0m);
    }
}

public abstract class RecipeInputValidator<T> : AbstractValidator<T> where T : IRecipeInput
{
    protected RecipeInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200);
        RuleFor(x => x.Description).NotNull();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PrepTime).GreaterThan(0);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.Difficulty).IsInEnum();
        RuleFor(x => x.Nutrition).SetValidator(new RecipeNutritionValidator()!);
    }
}

public sealed class CreateRecipeRequestValidator : RecipeInputValidator<CreateRecipeRequest>
{
    public CreateRecipeRequestValidator()
    {
        // FR-RCP-003 explicitly requires > 0, despite the model allowing zero.
        RuleFor(x => x.CookTime).GreaterThan(0);
    }
}

public sealed class UpdateRecipeRequestValidator : RecipeInputValidator<UpdateRecipeRequest>
{
    public UpdateRecipeRequestValidator()
    {
        // FR-RCP-004 has no stricter range: use the model's >= 0 constraint.
        RuleFor(x => x.CookTime).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Instructions).NotNull();
        RuleFor(x => x.RowVersion)
            .Must(IsCanonicalBase64)
            .WithMessage("RowVersion must be a non-empty canonical Base64 string.");
    }

    private static bool IsCanonicalBase64(string? value)
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
