using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;

namespace CulinaryBlog.Application.Categories.DTOs;

public sealed record CategoryDetailDto(
    CategoryDto Category,
    PagedResult<RecipeSummaryDto> Recipes
);