using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Images;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public sealed record DeleteRecipeImageCommand(Guid RecipeId, Guid ImageId, string RowVersion,
    string UserId, bool IsAdmin) : IRequest<RecipeImageDeleteResult>;

public sealed class DeleteRecipeImageCommandValidator : AbstractValidator<DeleteRecipeImageCommand>
{
    public DeleteRecipeImageCommandValidator()
        => RuleFor(x => x.RowVersion).Must(RecipeRowVersion.IsValid).WithMessage("RowVersion is not valid.");
}

public sealed class DeleteRecipeImageCommandHandler(IUnitOfWork unitOfWork, IImageCleanupQueue cleanup,
    TimeProvider clock) : IRequestHandler<DeleteRecipeImageCommand, RecipeImageDeleteResult>
{
    public async Task<RecipeImageDeleteResult> Handle(DeleteRecipeImageCommand command, CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.LoadForMutationAsync(unitOfWork.Recipes, command.RecipeId,
            command.UserId, command.IsAdmin, cancellationToken);
        var expected = RecipeRowVersion.Decode(command.RowVersion);
        recipe.EnsureCurrentRowVersion(expected);

        var image = recipe.GetImage(command.ImageId);
        var wasPrimary = image.IsPrimary;
        var objectKeys = RecipeImageStorageKey.ForImage(recipe.Id, image);

        await unitOfWork.Recipes.ExecuteInTransactionAsync(async token =>
        {
            // Step 1: physical delete frees the unique primary slot (SRS FR-RCP-008 step 13).
            unitOfWork.Recipes.RemoveImage(image);
            recipe.RemoveImage(image);
            await unitOfWork.SaveChangesAsync(token);

            // Step 2: promote the first remaining image (step 15) and advance the Recipe RowVersion.
            if (wasPrimary && recipe.NextPrimaryCandidate() is { } next)
                next.IsPrimary = true;
            recipe.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            unitOfWork.Recipes.Update(recipe, expected);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        // Only after the DB commit: a rolled-back delete must never remove files (step 14).
        foreach (var key in objectKeys)
            RecipeImageAccess.TryEnqueueDelete(cleanup, key);

        return new RecipeImageDeleteResult(RecipeRowVersion.Encode(recipe.RowVersion));
    }
}
