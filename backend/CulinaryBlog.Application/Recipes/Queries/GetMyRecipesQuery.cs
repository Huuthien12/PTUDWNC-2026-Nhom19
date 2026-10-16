using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries;

public sealed record GetMyRecipesQuery(
    string AuthorId,
    int Page = 1,
    int PageSize = 12) : IRequest<PagedResult<RecipeDashboardItemDto>>;

public sealed class GetMyRecipesQueryValidator : AbstractValidator<GetMyRecipesQuery>
{
    public GetMyRecipesQueryValidator()
    {
        RuleFor(x => x.AuthorId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetMyRecipesQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetMyRecipesQuery, PagedResult<RecipeDashboardItemDto>>
{
    public async Task<PagedResult<RecipeDashboardItemDto>> Handle(
        GetMyRecipesQuery request,
        CancellationToken cancellationToken)
    {
        var totalCount = await unitOfWork.Recipes.CountMineAsync(
            request.AuthorId,
            cancellationToken);
        var items = await unitOfWork.Recipes.GetMineAsync(
            request.AuthorId,
            request.Page,
            request.PageSize,
            cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResult<RecipeDashboardItemDto>(
            items,
            totalCount,
            request.Page,
            request.PageSize,
            totalPages);
    }
}
