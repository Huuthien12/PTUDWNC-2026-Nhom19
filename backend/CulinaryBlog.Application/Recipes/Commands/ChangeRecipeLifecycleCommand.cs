using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public enum RecipeLifecycleAction { Publish, Unpublish, Archive }
public sealed record ChangeRecipeLifecycleCommand(Guid Id, RecipeLifecycleAction Action, string RowVersion, string UserId, bool IsAdmin) : IRequest<RecipeDto>;
public sealed class ChangeRecipeLifecycleCommandValidator : AbstractValidator<ChangeRecipeLifecycleCommand>
{
    public ChangeRecipeLifecycleCommandValidator() => RuleFor(x => x.RowVersion).Must(value => { try { var bytes = Convert.FromBase64String(value); return bytes.Length > 0 && Convert.ToBase64String(bytes) == value; } catch { return false; } });
}
public sealed class ChangeRecipeLifecycleCommandHandler(IUnitOfWork unitOfWork, ICacheService cache, IRecipeCacheInvalidator? invalidator = null) : IRequestHandler<ChangeRecipeLifecycleCommand, RecipeDto>
{
    public async Task<RecipeDto> Handle(ChangeRecipeLifecycleCommand command, CancellationToken cancellationToken)
    {
        var recipe = await unitOfWork.Recipes.GetForLifecycleAsync(command.Id, cancellationToken) ?? throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe not found.");
        if (!command.IsAdmin && recipe.AuthorId != command.UserId) throw new ForbiddenException("RECIPE_FORBIDDEN", "Only the author or an Admin may change this recipe.");
        var wasPublished = recipe.Status == RecipeStatus.Published;
        switch (command.Action) { case RecipeLifecycleAction.Publish: recipe.Publish(); break; case RecipeLifecycleAction.Unpublish: recipe.Unpublish(); break; default: recipe.Archive(); break; }
        var invalidateCategoryCounts = wasPublished != (recipe.Status == RecipeStatus.Published);
        unitOfWork.Recipes.Update(recipe, Convert.FromBase64String(command.RowVersion));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // No outer transaction: SaveChanges has committed before eviction.
        // Idempotent actions still save a new RowVersion, so their cached DTO must also be evicted.
        await cache.RemoveSafelyAsync($"recipe:{recipe.Slug}", cancellationToken);
        if (invalidator is not null) await invalidator.ListChangedAsync(cancellationToken);
        if (invalidateCategoryCounts)
            await cache.RemoveSafelyAsync("categories:all", cancellationToken);
        return new RecipeDto(recipe.Id, recipe.Title, recipe.Slug, recipe.Description, recipe.Instructions, recipe.CategoryId, recipe.AuthorId, recipe.PrepTime, recipe.CookTime, recipe.Servings, recipe.Difficulty, recipe.Status, new(recipe.Nutrition.Calories, recipe.Nutrition.Protein, recipe.Nutrition.Carbs, recipe.Nutrition.Fat), Convert.ToBase64String(recipe.RowVersion), recipe.CreatedAt, recipe.UpdatedAt, recipe.PublishedAt);
    }
}
