using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Mappers;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record GetRecipeDetailQuery(string Slug, string? UserId, bool IsAdmin)
    : IRequest<RecipeDetailDto>;

public sealed class GetRecipeDetailQueryValidator : AbstractValidator<GetRecipeDetailQuery>
{
    public GetRecipeDetailQueryValidator() => RuleFor(query => query.Slug).NotEmpty();
}

public sealed class GetRecipeDetailQueryHandler(
    IRecipeRepository recipes,
    ICacheService cache,
    IOptions<RecipeDetailCacheOptions> options,
    ILogger<GetRecipeDetailQueryHandler>? logger = null)
    : IRequestHandler<GetRecipeDetailQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(GetRecipeDetailQuery query, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return await ReadDetailAsync(query, cancellationToken);

        var access = await recipes.GetDetailAccessBySlugAsync(
            query.Slug, query.UserId, query.IsAdmin, cancellationToken);
        if (access is null)
            throw new NotFoundException("RECIPE_NOT_FOUND", "The requested recipe does not exist.");

        if (access.Status != RecipeStatus.Published)
            return await ReadDetailAsync(query, cancellationToken);

        var key = $"recipe:{access.Slug}";
        RecipeDetailDto? cached = null;
        try { cached = await cache.GetAsync<RecipeDetailDto>(key, cancellationToken); }
        catch (Exception error) when (CacheFailure.IsInfrastructure(error))
        { CacheFailure.Log(logger, "recipe-detail read", error); }
        // DTO versions are canonical Base64 strings; compare the byte value via the same encoding.
        if (cached is not null &&
            cached.Id == access.Id &&
            cached.Slug == access.Slug &&
            cached.Status == RecipeStatus.Published &&
            cached.RowVersion == Convert.ToBase64String(access.RowVersion))
        {
            logger?.LogDebug("Recipe Detail cache hit for {RecipeId}", access.Id);
            return cached;
        }

        logger?.LogDebug("Recipe Detail cache miss or version mismatch for {RecipeId}", access.Id);

        // Re-run the existing authorized query: visibility can change between these reads.
        var detail = await ReadDetailAsync(query, cancellationToken);
        if (detail.Status == RecipeStatus.Published)
        {
            try
            {
                await cache.SetAsync($"recipe:{detail.Slug}", detail, TimeSpan.FromMinutes(5),
                    cancellationToken, useAbsoluteExpiration: true);
            }
            catch (Exception error) when (CacheFailure.IsInfrastructure(error))
            { CacheFailure.Log(logger, "recipe-detail write", error); }
        }

        return detail;
    }

    private async Task<RecipeDetailDto> ReadDetailAsync(
        GetRecipeDetailQuery query, CancellationToken cancellationToken)
    {
        var recipe = await recipes.GetBySlugAsync(query.Slug, query.UserId, query.IsAdmin, cancellationToken);
        if (recipe is null)
            throw new NotFoundException("RECIPE_NOT_FOUND", "The requested recipe does not exist.");
        return RecipeDetailMapper.ToDto(recipe);
    }
}
