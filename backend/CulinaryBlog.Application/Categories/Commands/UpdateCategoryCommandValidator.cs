using FluentValidation;

namespace CulinaryBlog.Application.Categories.Commands;

public sealed class UpdateCategoryCommandValidator
    : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(50)
            .Must(NotContainHtml)
            .WithMessage("Name must not contain HTML.");
    }

    private static bool NotContainHtml(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        return !name.Contains('<') &&
               !name.Contains('>');
    }
}