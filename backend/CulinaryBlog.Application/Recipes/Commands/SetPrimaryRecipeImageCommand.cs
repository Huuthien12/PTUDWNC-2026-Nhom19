using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Images;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public sealed record SetPrimaryRecipeImageCommand(Guid RecipeId, Guid ImageId, string RowVersion,
    string UserId, bool IsAdmin) : IRequest<RecipeImageDto>;

public sealed class SetPrimaryRecipeImageCommandValidator : AbstractValidator<SetPrimaryRecipeImageCommand>
{
    public SetPrimaryRecipeImageCommandValidator()
        => RuleFor(x => x.RowVersion).Must(RecipeRowVersion.IsValid).WithMessage("RowVersion is not valid.");
}

public sealed class SetPrimaryRecipeImageCommandHandler(IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SetPrimaryRecipeImageCommand, RecipeImageDto>
{
    public async Task<RecipeImageDto> Handle(SetPrimaryRecipeImageCommand command, CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.LoadForMutationAsync(unitOfWork.Recipes, command.RecipeId,
            command.UserId, command.IsAdmin, cancellationToken);
        var expected = RecipeRowVersion.Decode(command.RowVersion);
        recipe.EnsureCurrentRowVersion(expected);

        // 404 when the image does not belong to this recipe.
        var image = recipe.GetImage(command.ImageId);

        await unitOfWork.Recipes.ExecuteInTransactionAsync(async token =>
        {
            // The partial unique index (RecipeId) WHERE IsPrimary allows one primary per recipe,
            // so demote first, then promote; both steps commit or roll back together.
            if (recipe.DemoteOtherPrimaryImages(image))
                await unitOfWork.SaveChangesAsync(token);

            recipe.PromoteImage(image);
            recipe.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            unitOfWork.Recipes.Update(recipe, expected);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        return RecipeImageDto.From(image, recipe.RowVersion);
    }
}
