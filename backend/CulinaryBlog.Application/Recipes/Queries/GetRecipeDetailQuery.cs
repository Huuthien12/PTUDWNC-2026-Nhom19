using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Mappers;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record GetRecipeDetailQuery(string Slug, string? UserId, bool IsAdmin)
    : IRequest<RecipeDetailDto>;

public sealed class GetRecipeDetailQueryValidator : AbstractValidator<GetRecipeDetailQuery>
{
    public GetRecipeDetailQueryValidator() => RuleFor(query => query.Slug).NotEmpty();
}

public sealed class GetRecipeDetailQueryHandler(IRecipeRepository recipes)
    : IRequestHandler<GetRecipeDetailQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(GetRecipeDetailQuery query, CancellationToken cancellationToken)
    {
        var recipe = await recipes.GetBySlugAsync(query.Slug, query.UserId, query.IsAdmin, cancellationToken);
        if (recipe is null)
            throw new NotFoundException("RECIPE_NOT_FOUND", "The requested recipe does not exist.");
        return RecipeDetailMapper.ToDto(recipe);
    }
}
