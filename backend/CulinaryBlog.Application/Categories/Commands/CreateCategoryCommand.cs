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
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CategoryDto> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var nameExists =
            await _unitOfWork.Categories.NameExistsAsync(
                name,
                cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException(
                "CATEGORY_NAME_EXISTS");
        }

        var baseSlug = SlugHelper.Generate(name);
        var slug = baseSlug;
        var suffix = 2;

        while (await _unitOfWork.Categories.SlugExistsAsync(
                   slug,
                   cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        var category = Category.Create(
            name,
            slug,
            request.Description);

        await _unitOfWork.Categories.AddAsync(
            category,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description);
    }
}