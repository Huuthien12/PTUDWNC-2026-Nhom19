using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries;

public sealed record GetCategoryBySlugQuery(
    string Slug,
    int Page = 1,
    int PageSize = 12,
    string? UserId = null,
    bool IsAdmin = false
) : IRequest<CategoryDetailDto?>;

public sealed class GetCategoryBySlugQueryHandler
    : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCategoryBySlugQueryHandler(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CategoryDetailDto?> Handle(
        GetCategoryBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var category =
            await _unitOfWork.Categories.GetBySlugAsync(
                request.Slug,
                cancellationToken);

        if (category is null)
        {
            return null;
        }

        var totalCount =
            await _unitOfWork.Recipes.CountByCategoryAsync(
                category.Id,
                request.UserId,
                request.IsAdmin,
                cancellationToken);

        var recipes =
            await _unitOfWork.Recipes.GetByCategoryAsync(
                category.Id,
                request.Page,
                request.PageSize,
                request.UserId,
                request.IsAdmin,
                cancellationToken);

        var recipeDtos = recipes
            .Select(recipe =>
            {
                var primaryImage = recipe.Images
                    .Where(image =>
                        image.IsPrimary &&
                        !image.IsDeleted)
                    .OrderBy(image => image.OrderIndex)
                    .FirstOrDefault();

                var thumbnailUrl =
                    primaryImage?.ThumbnailUrl
                    ?? primaryImage?.OriginalUrl;

                return new RecipeSummaryDto(
                    recipe.Id,
                    recipe.Title,
                    recipe.Slug,
                    recipe.Description,
                    recipe.PrepTime,
                    recipe.CookTime,
                    recipe.Servings,
                    recipe.Difficulty,
                    thumbnailUrl,
                    recipe.PublishedAt);
            })
            .ToList();

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)request.PageSize);

        var categoryDto = new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description);

        var pagedRecipes =
            new PagedResult<RecipeSummaryDto>(
                recipeDtos,
                totalCount,
                request.Page,
                request.PageSize,
                totalPages);

        return new CategoryDetailDto(
            categoryDto,
            pagedRecipes);
    }
}