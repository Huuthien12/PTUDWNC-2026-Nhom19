using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Common.Models;

public sealed record RecipeDetailAccessResult(
    Guid Id,
    string Slug,
    RecipeStatus Status,
    byte[] RowVersion);
