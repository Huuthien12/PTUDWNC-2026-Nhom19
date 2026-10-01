using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public sealed record DeleteRecipeCommand(Guid Id, string RowVersion, string UserId, bool IsAdmin) : IRequest;

public sealed class DeleteRecipeCommandValidator : AbstractValidator<DeleteRecipeCommand>
{
    public DeleteRecipeCommandValidator() => RuleFor(x => x.RowVersion).Must(value =>
    {
        try { var bytes = Convert.FromBase64String(value); return bytes.Length > 0 && Convert.ToBase64String(bytes) == value; }
        catch { return false; }
    });
}

public sealed class DeleteRecipeCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteRecipeCommand>
{
    public async Task Handle(DeleteRecipeCommand command, CancellationToken cancellationToken)
    {
        var recipe = await unitOfWork.Recipes.GetForLifecycleAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe not found.");
        if (!command.IsAdmin && recipe.AuthorId != command.UserId)
            throw new ForbiddenException("RECIPE_FORBIDDEN", "Only the author or an Admin may delete this recipe.");

        recipe.SoftDelete();
        unitOfWork.Recipes.Update(recipe, Convert.FromBase64String(command.RowVersion));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
