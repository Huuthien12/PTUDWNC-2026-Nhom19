using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands;

public sealed record DeleteCategoryCommand(
    Guid Id
) : IRequest<DeleteCategoryResult>;

public sealed record DeleteCategoryResult(
    bool Found,
    int RecipeCount
);

public sealed class DeleteCategoryCommandHandler
    : IRequestHandler<DeleteCategoryCommand, DeleteCategoryResult>
{
    private const string CategoriesCacheKey = "categories:all";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public DeleteCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<DeleteCategoryResult> Handle(
        DeleteCategoryCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Tìm Category
        var category =
            await _unitOfWork.Categories.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (category is null)
        {
            return new DeleteCategoryResult(
                Found: false,
                RecipeCount: 0);
        }

        // 2. Kiểm tra Category còn Recipe hay không
        var recipeCount =
            await _unitOfWork.Recipes.CountAllByCategoryAsync(
                category.Id,
                cancellationToken);

        if (recipeCount > 0)
        {
            return new DeleteCategoryResult(
                Found: true,
                RecipeCount: recipeCount);
        }

        // 3. Xóa Category
        _unitOfWork.Categories.Delete(category);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        // 4. Invalidate Redis
        await _cacheService.RemoveAsync(
            CategoriesCacheKey,
            cancellationToken);

        return new DeleteCategoryResult(
            Found: true,
            RecipeCount: 0);
    }
}