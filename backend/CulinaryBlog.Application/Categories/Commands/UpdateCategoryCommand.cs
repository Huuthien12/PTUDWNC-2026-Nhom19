using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description
) : IRequest<CategoryDto?>;

public sealed class UpdateCategoryCommandHandler
    : IRequestHandler<UpdateCategoryCommand, CategoryDto?>
{
    private const string CategoriesCacheKey = "categories:all";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public UpdateCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<CategoryDto?> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        // Tìm Category theo ID
        var category =
            await _unitOfWork.Categories.GetByIdAsync(
                request.Id,
                cancellationToken);

        if (category is null)
        {
            return null;
        }

        var name = request.Name.Trim();

        // Chỉ kiểm tra trùng khi Name thực sự thay đổi
        if (!string.Equals(
                category.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
        {
            var nameExists =
                await _unitOfWork.Categories.NameExistsAsync(
                    name,
                    cancellationToken);

            if (nameExists)
            {
                throw new InvalidOperationException(
                    "CATEGORY_NAME_EXISTS");
            }
        }

        // Cập nhật Name và Description
        category.Name = name;

        category.Description =
            string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();

        category.UpdatedAt = DateTime.UtcNow;

        // Theo SRS:
        // Slug KHÔNG thay đổi khi đổi tên Category.

        _unitOfWork.Categories.Update(category);

        // Lưu thay đổi vào database
        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        // Category đã thay đổi -> xóa cache danh sách cũ
        await _cacheService.RemoveAsync(
            CategoriesCacheKey,
            cancellationToken);

        // recipeCount chỉ tính các Recipe Published
        var recipeCount =
            await _unitOfWork.Recipes
                .CountPublishedByCategoryAsync(
                    category.Id,
                    cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            recipeCount);
    }
}