using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record GetRecipeSuggestionsQuery(
    string Query
) : IRequest<RecipeSuggestionsDto>;

public sealed class GetRecipeSuggestionsQueryHandler
    : IRequestHandler<GetRecipeSuggestionsQuery, RecipeSuggestionsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<GetRecipeSuggestionsQueryHandler> _logger;

    public GetRecipeSuggestionsQueryHandler(
        IUnitOfWork unitOfWork,
        ICacheService cache,
        ILogger<GetRecipeSuggestionsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
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
        catch (Exception exception) when (exception.GetType().Namespace == "StackExchange.Redis")
        {
            _logger.LogWarning(exception, "Suggestions cache read failed.");
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
        catch (Exception exception) when (exception.GetType().Namespace == "StackExchange.Redis")
        {
            _logger.LogWarning(exception, "Suggestions cache write failed.");
        }

        return result;
    }
}
