using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record GetRecipesQuery(
    int Page = 1,
    int PageSize = 12,
    string? UserId = null,
    bool IsAdmin = false,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string SortBy = "createdAt",
    string SortOrder = "desc"
) : IRequest<PagedResult<RecipeSummaryDto>>;

public sealed class GetRecipesQueryHandler
    : IRequestHandler<GetRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetRecipesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        GetRecipesQuery request,
        CancellationToken cancellationToken)
    {
        var totalCount = await _unitOfWork.Recipes.CountVisibleAsync(
            request.UserId,
            request.IsAdmin,
            request.CategoryId,
            request.Difficulty,
            request.MaxCookTime,
            request.MinServings,
            cancellationToken);

        var recipes = await _unitOfWork.Recipes.GetVisibleAsync(
            request.Page,
            request.PageSize,
            request.UserId,
            request.IsAdmin,
            request.CategoryId,
            request.Difficulty,
            request.MaxCookTime,
            request.MinServings,
            request.SortBy,
            request.SortOrder,
            cancellationToken);

        var items = recipes.Select(recipe =>
        {
            var image = recipe.Images
                .Where(image =>
                    image.IsPrimary &&
                    !image.IsDeleted)
                .OrderBy(image => image.OrderIndex)
                .FirstOrDefault();

            return new RecipeSummaryDto(
                recipe.Id,
                recipe.Title,
                recipe.Slug,
                recipe.Description,
                recipe.PrepTime,
                recipe.CookTime,
                recipe.Servings,
                recipe.Difficulty,
                image?.ThumbnailUrl ?? image?.OriginalUrl,
                recipe.PublishedAt);
        }).ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(
                totalCount / (double)request.PageSize);

        return new PagedResult<RecipeSummaryDto>(
            items,
            totalCount,
            request.Page,
            request.PageSize,
            totalPages);
    }
}