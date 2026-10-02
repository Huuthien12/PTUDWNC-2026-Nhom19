using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record GetRecipeSuggestionsQuery(
    string Query
) : IRequest<RecipeSuggestionsDto>;

public sealed class GetRecipeSuggestionsQueryHandler
    : IRequestHandler<GetRecipeSuggestionsQuery, RecipeSuggestionsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public GetRecipeSuggestionsQueryHandler(
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<RecipeSuggestionsDto> Handle(
        GetRecipeSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query.Trim().ToLowerInvariant();

        var cacheKey = $"search:suggest:{normalizedQuery}";

        try
        {
            var cached = await _cache.GetAsync<RecipeSuggestionsDto>(
                cacheKey,
                cancellationToken);

            if (cached is not null)
            {
                return cached;
            }
        }
        catch
        {
            // Redis không kh? d?ng:
            // ti?p t?c query PostgreSQL theo SRS A3.
        }

        var items = await _unitOfWork.Recipes.GetSuggestionsAsync(
            normalizedQuery,
            cancellationToken);

        var result = new RecipeSuggestionsDto(items);

        try
        {
            await _cache.SetAsync(
                cacheKey,
                result,
                TimeSpan.FromMinutes(1),
                cancellationToken);
        }
        catch
        {
            // Redis l?i không ðý?c làm request Suggestions th?t b?i.
        }

        return result;
    }
}
