using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands;

public sealed record CreateCategoryCommand(
    string Name,
    string? Description
) : IRequest<CategoryDto>;

public sealed class CreateCategoryCommandHandler
    : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private const string CategoriesCacheKey = "categories:all";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public CreateCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<CategoryDto> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        // Kiểm tra tên Category đã tồn tại
        var nameExists =
            await _unitOfWork.Categories.NameExistsAsync(
                name,
                cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException(
                "CATEGORY_NAME_EXISTS");
        }

        // Tạo slug
        var baseSlug = SlugHelper.Generate(name);
        var slug = baseSlug;
        var suffix = 2;

        // Nếu slug trùng thì thêm -2, -3, ...
        while (await _unitOfWork.Categories.SlugExistsAsync(
                   slug,
                   cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        // Tạo Category
        var category = Category.Create(
            name,
            slug,
            request.Description);

        await _unitOfWork.Categories.AddAsync(
            category,
            cancellationToken);

        // Lưu database
        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        // Category đã thay đổi -> xóa cache cũ
        await _cacheService.RemoveAsync(
            CategoriesCacheKey,
            cancellationToken);

        // Category mới chưa có recipe
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            0);
    }
}