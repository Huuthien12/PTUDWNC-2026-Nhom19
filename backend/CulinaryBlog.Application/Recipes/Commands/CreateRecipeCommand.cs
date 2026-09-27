using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public sealed record CreateRecipeCommand(CreateRecipeRequest Request, string AuthorId) : IRequest<RecipeDto>;

public sealed class RecipeSlugExistsException : Exception;

public sealed class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator(IValidator<CreateRecipeRequest> validator)
    {
        RuleFor(x => x.Request).NotNull().SetValidator(validator);
        RuleFor(x => x.AuthorId).NotEmpty();
    }
}

public sealed class CreateRecipeCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRecipeCommand, RecipeDto>
{
    public async Task<RecipeDto> Handle(CreateRecipeCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (await unitOfWork.Categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            throw new ValidationException([new("CategoryId", "Category không hợp lệ.")]);

        var slug = SlugHelper.Generate(request.Title);
        if (string.IsNullOrEmpty(slug))
            throw new ValidationException([new("Title", "Title must produce a non-empty slug.")]);
        if (await unitOfWork.Recipes.SlugExistsAsync(slug, cancellationToken: cancellationToken))
            throw new RecipeSlugExistsException();

        var recipe = new Recipe
        {
            Title = request.Title, Slug = slug, Description = request.Description,
            Instructions = request.Instructions ?? string.Empty,
            CategoryId = request.CategoryId, AuthorId = command.AuthorId,
            PrepTime = request.PrepTime, CookTime = request.CookTime, Servings = request.Servings,
            Difficulty = request.Difficulty, Status = RecipeStatus.Draft,
            Nutrition = new RecipeNutrition
            {
                Calories = request.Nutrition?.Calories, Protein = request.Nutrition?.Protein,
                Carbs = request.Nutrition?.Carbs, Fat = request.Nutrition?.Fat
            }
        };
        await unitOfWork.Recipes.AddAsync(recipe, cancellationToken);
        // Owned nutrition and the root are persisted by one atomic SaveChanges.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RecipeDto(recipe.Id, recipe.Title, recipe.Slug, recipe.Description,
            recipe.Instructions, recipe.CategoryId, recipe.AuthorId, recipe.PrepTime,
            recipe.CookTime, recipe.Servings, recipe.Difficulty, recipe.Status,
            new(recipe.Nutrition.Calories, recipe.Nutrition.Protein, recipe.Nutrition.Carbs, recipe.Nutrition.Fat),
            Convert.ToBase64String(recipe.RowVersion), recipe.CreatedAt, recipe.UpdatedAt, recipe.PublishedAt);
    }
}
