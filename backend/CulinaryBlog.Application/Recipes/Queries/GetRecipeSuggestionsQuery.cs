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
    private readonly IRecipeCacheInvalidator _invalidator;
    private readonly ILogger<GetRecipeSuggestionsQueryHandler> _logger;

    public GetRecipeSuggestionsQueryHandler(
        IUnitOfWork unitOfWork,
        ICacheService cache,
        IRecipeCacheInvalidator invalidator,
        ILogger<GetRecipeSuggestionsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _invalidator = invalidator;
        _logger = logger;
    }

    public async Task<RecipeSuggestionsDto> Handle(
        GetRecipeSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query.Trim().ToLowerInvariant();

        var version = await _invalidator.GetSuggestionVersionAsync(cancellationToken);
        var cacheKey = version is null ? null : $"search:suggest:v{version}:{normalizedQuery}";

        if (cacheKey is not null) try
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
            CacheFailure.Log(_logger, "suggestions read", exception);
        }

        var items = await _unitOfWork.Recipes.GetSuggestionsAsync(
            normalizedQuery,
            cancellationToken);

        var result = new RecipeSuggestionsDto(items);

        if (cacheKey is not null) try
        {
            await _cache.SetAsync(
                cacheKey,
                result,
                TimeSpan.FromMinutes(1),
                cancellationToken);
        }
        catch (Exception exception) when (exception.GetType().Namespace == "StackExchange.Redis")
        {
            CacheFailure.Log(_logger, "suggestions write", exception);
        }

        return result;
    }
}
