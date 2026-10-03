using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Models;

public sealed record SearchRecipeResult(Recipe Recipe, double RelevanceScore);
