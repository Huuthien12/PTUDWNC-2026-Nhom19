using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record SearchRecipesQuery(string Q, int Page = 1, int PageSize = 12)
    : IRequest<PagedResult<RecipeSearchDto>>;

public sealed class SearchRecipesQueryValidator : AbstractValidator<SearchRecipesQuery>
{
    public SearchRecipesQueryValidator()
    {
        RuleFor(x => x.Q).Must(q => q is not null && q.Trim().Length >= 2)
            .WithMessage("Search query must contain at least 2 characters.")
            .MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class SearchRecipesQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSearchDto>>
{
    public async Task<PagedResult<RecipeSearchDto>> Handle(
        SearchRecipesQuery request, CancellationToken cancellationToken)
    {
        var tsQuery = BuildPrefixQuery(request.Q);
        if (tsQuery.Length == 0)
            throw new ValidationException("Search query must contain searchable terms.");

        var totalCount = await unitOfWork.Recipes.CountSearchAsync(tsQuery, cancellationToken);
        var results = await unitOfWork.Recipes.SearchAsync(
            tsQuery, request.Page, request.PageSize, cancellationToken);
        var items = results.Select(result =>
        {
            var image = result.Recipe.Images
                .Where(item => item.IsPrimary && !item.IsDeleted)
                .OrderBy(item => item.OrderIndex)
                .FirstOrDefault();
            return new RecipeSearchDto(
                result.Recipe.Id, result.Recipe.Title, result.Recipe.Slug,
                result.Recipe.Description, result.Recipe.PrepTime, result.Recipe.CookTime,
                result.Recipe.Servings, result.Recipe.Difficulty,
                image?.ThumbnailUrl ?? image?.OriginalUrl, result.Recipe.PublishedAt,
                result.RelevanceScore);
        }).ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);
        return new PagedResult<RecipeSearchDto>(
            items, totalCount, request.Page, request.PageSize, totalPages);
    }

    private static string BuildPrefixQuery(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormC)
            .ToLower(CultureInfo.InvariantCulture);
        var tokens = Regex.Matches(normalized, @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .Where(token => token.Length > 0)
            .Select(token => token + ":*");
        return string.Join(" & ", tokens);
    }
}
