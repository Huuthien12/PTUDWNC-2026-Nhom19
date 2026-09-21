using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries;

public sealed record GetCategoriesQuery
    : IRequest<IReadOnlyList<CategoryDto>>;

public sealed class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private const string CacheKey = "categories:all";

    private static readonly TimeSpan CacheExpiration =
        TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public GetCategoriesQueryHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Check Redis
        var cachedCategories =
            await _cacheService.GetAsync<List<CategoryDto>>(
                CacheKey,
                cancellationToken);

        if (cachedCategories is not null)
        {
            return cachedCategories;
        }

        // 2. Cache miss -> Database
        var categories =
            await _unitOfWork.Categories
                .GetAllWithRecipeCountAsync(cancellationToken);

        var result = categories
            .Select(item =>
                new CategoryDto(
                    item.Category.Id,
                    item.Category.Name,
                    item.Category.Slug,
                    item.Category.Description,
                    item.RecipeCount))
            .ToList();

        // 3. Save Redis - sliding expiration 30 minutes
        await _cacheService.SetAsync(
            CacheKey,
            result,
            CacheExpiration,
            cancellationToken);

        return result;
    }
}