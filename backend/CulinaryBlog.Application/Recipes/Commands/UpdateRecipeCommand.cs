using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

// Identity and role are supplied by the authenticated endpoint, never by request JSON.
public sealed record UpdateRecipeCommand(Guid Id, UpdateRecipeRequest Request, string UserId, bool IsAdmin)
    : IRequest<RecipeDto>;

public sealed class RecipeNotFoundException : Exception;
public sealed class RecipeForbiddenException : Exception;

public sealed class UpdateRecipeCommandHandler(IUnitOfWork unitOfWork,
    IValidator<UpdateRecipeRequest> validator, ICacheService cache, TimeProvider clock)
    : IRequestHandler<UpdateRecipeCommand, RecipeDto>
{
    public async Task<RecipeDto> Handle(UpdateRecipeCommand command, CancellationToken cancellationToken)
    {
        var recipe = await unitOfWork.Recipes.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new RecipeNotFoundException();
        if (!command.IsAdmin && recipe.AuthorId != command.UserId)
            throw new RecipeForbiddenException();

        // Validate only after resource authorization, and before any tracked entity mutation.
        var request = command.Request;
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        if (await unitOfWork.Categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            throw new ValidationException([new("CategoryId", "Category không hợp lệ.")]);

        var invalidateCategoryCounts = recipe.Status == RecipeStatus.Published && recipe.CategoryId != request.CategoryId;
        recipe.Title = request.Title;
        recipe.Description = request.Description;
        recipe.Instructions = request.Instructions;
        recipe.CategoryId = request.CategoryId;
        recipe.PrepTime = request.PrepTime;
        recipe.CookTime = request.CookTime;
        recipe.Servings = request.Servings;
        recipe.Difficulty = request.Difficulty;
        // Slug remains stable when title changes. Lifecycle, owner and children are not mapped.
        if (request.Nutrition is { } nutrition)
        {
            recipe.Nutrition.Calories = nutrition.Calories;
            recipe.Nutrition.Protein = nutrition.Protein;
            recipe.Nutrition.Carbs = nutrition.Carbs;
            recipe.Nutrition.Fat = nutrition.Fat;
        }
        recipe.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        unitOfWork.Recipes.Update(recipe, Convert.FromBase64String(request.RowVersion));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // This is the existing cache affected by moving a Published recipe between categories.
        // Recipe output-cache tags do not exist yet; do not invent unused cache keys.
        if (invalidateCategoryCounts)
            await cache.RemoveAsync("categories:all", cancellationToken);

        return new RecipeDto(recipe.Id, recipe.Title, recipe.Slug, recipe.Description,
            recipe.Instructions, recipe.CategoryId, recipe.AuthorId, recipe.PrepTime,
            recipe.CookTime, recipe.Servings, recipe.Difficulty, recipe.Status,
            new(recipe.Nutrition.Calories, recipe.Nutrition.Protein, recipe.Nutrition.Carbs, recipe.Nutrition.Fat),
            Convert.ToBase64String(recipe.RowVersion), recipe.CreatedAt, recipe.UpdatedAt, recipe.PublishedAt);
    }
}
