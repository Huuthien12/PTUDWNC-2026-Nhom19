using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public sealed record AddRecipeIngredientCommand(
    Guid RecipeId,
    CreateRecipeIngredientRequest Request,
    string UserId,
    bool IsAdmin) : IRequest<RecipeIngredientDto>;

public sealed record UpdateRecipeIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    UpdateRecipeIngredientRequest Request,
    string UserId,
    bool IsAdmin) : IRequest<RecipeIngredientDto>;

public sealed record DeleteRecipeIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    string RowVersion,
    string UserId,
    bool IsAdmin) : IRequest;

public sealed class AddRecipeIngredientCommandHandler(
    IUnitOfWork unitOfWork) : IRequestHandler<AddRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(
        AddRecipeIngredientCommand command,
        CancellationToken cancellationToken)
    {
        var recipe = await GetAuthorizedRecipe(command.RecipeId, command.UserId, command.IsAdmin, cancellationToken);
        var request = command.Request;
        var ingredient = new RecipeIngredient
        {
            RecipeId = recipe.Id,
            Name = request.Name,
            Quantity = request.Quantity,
            Unit = request.Unit,
            Notes = request.Notes,
            OrderIndex = request.SortOrder
        };
        recipe.Ingredients.Add(ingredient);
        unitOfWork.Recipes.UpdateForChildMutation(recipe, Decode(request.RowVersion));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(ingredient, recipe);
    }

    private async Task<Recipe> GetAuthorizedRecipe(
        Guid recipeId, string userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var recipe = await unitOfWork.Recipes.GetForLifecycleAsync(recipeId, cancellationToken)
            ?? throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe not found.");
        if (!isAdmin && recipe.AuthorId != userId)
            throw new ForbiddenException("RECIPE_FORBIDDEN", "Only the author or an Admin may update ingredients.");
        return recipe;
    }

    internal static byte[] Decode(string rowVersion) => Convert.FromBase64String(rowVersion);

    internal static RecipeIngredientDto ToDto(RecipeIngredient ingredient, Recipe recipe) =>
        new(ingredient.Id, ingredient.Name, ingredient.Quantity, ingredient.Unit ?? string.Empty,
            ingredient.Notes, ingredient.OrderIndex, Convert.ToBase64String(recipe.RowVersion));
}

public sealed class UpdateRecipeIngredientCommandHandler(
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(
        UpdateRecipeIngredientCommand command,
        CancellationToken cancellationToken)
    {
        var recipe = await unitOfWork.Recipes.GetForLifecycleAsync(command.RecipeId, cancellationToken)
            ?? throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe not found.");
        if (!command.IsAdmin && recipe.AuthorId != command.UserId)
            throw new ForbiddenException("RECIPE_FORBIDDEN", "Only the author or an Admin may update ingredients.");
        var ingredient = recipe.Ingredients.SingleOrDefault(item => item.Id == command.IngredientId)
            ?? throw new NotFoundException("RECIPE_INGREDIENT_NOT_FOUND", "Recipe ingredient not found.");
        var request = command.Request;
        ingredient.Name = request.Name;
        ingredient.Quantity = request.Quantity;
        ingredient.Unit = request.Unit;
        ingredient.Notes = request.Notes;
        ingredient.OrderIndex = request.SortOrder;
        unitOfWork.Recipes.UpdateForChildMutation(recipe, AddRecipeIngredientCommandHandler.Decode(request.RowVersion));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AddRecipeIngredientCommandHandler.ToDto(ingredient, recipe);
    }
}

public sealed class DeleteRecipeIngredientCommandHandler(
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteRecipeIngredientCommand>
{
    public async Task Handle(DeleteRecipeIngredientCommand command, CancellationToken cancellationToken)
    {
        var recipe = await unitOfWork.Recipes.GetForLifecycleAsync(command.RecipeId, cancellationToken)
            ?? throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe not found.");
        if (!command.IsAdmin && recipe.AuthorId != command.UserId)
            throw new ForbiddenException("RECIPE_FORBIDDEN", "Only the author or an Admin may update ingredients.");
        var ingredient = recipe.Ingredients.SingleOrDefault(item => item.Id == command.IngredientId)
            ?? throw new NotFoundException("RECIPE_INGREDIENT_NOT_FOUND", "Recipe ingredient not found.");
        ingredient.IsDeleted = true;
        ingredient.UpdatedAt = DateTime.UtcNow;
        unitOfWork.Recipes.UpdateForChildMutation(recipe, AddRecipeIngredientCommandHandler.Decode(command.RowVersion));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
