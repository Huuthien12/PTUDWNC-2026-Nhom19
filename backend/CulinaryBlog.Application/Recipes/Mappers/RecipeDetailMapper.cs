using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.Mappers;

public static class RecipeDetailMapper
{
    public static RecipeDetailDto ToDto(Recipe recipe)
    {
        return new RecipeDetailDto
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Slug = recipe.Slug,
            Description = recipe.Description,
            Instructions = recipe.Instructions,

            PrepTime = recipe.PrepTime,
            CookTime = recipe.CookTime,
            Servings = recipe.Servings,

            Difficulty = recipe.Difficulty,
            Status = recipe.Status,
            RowVersion = Convert.ToBase64String(recipe.RowVersion),
            CreatedAt = recipe.CreatedAt,
            PublishedAt = recipe.PublishedAt,

            Category = new RecipeCategoryDto
            {
                Id = recipe.Category.Id,
                Name = recipe.Category.Name,
                Slug = recipe.Category.Slug
            },

            Author = new AuthorDto
            {
                Id = recipe.Author.Id,
                FullName = recipe.Author.FullName,
                AvatarUrl = recipe.Author.AvatarUrl
            },

            Nutrition = recipe.Nutrition == null
                ? null
                : new NutritionDto
                {
                    Calories = recipe.Nutrition.Calories,
                    Protein = recipe.Nutrition.Protein,
                    Carbs = recipe.Nutrition.Carbs,
                    Fat = recipe.Nutrition.Fat
                },

            Ingredients = recipe.Ingredients
                .Select(i => new IngredientDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Notes = i.Notes,
                    OrderIndex = i.OrderIndex
                })
                .ToList(),

            Steps = recipe.Steps
                .Select(s => new StepDto
                {
                    Id = s.Id,
                    StepNumber = s.StepNumber,
                    Title = s.Title,
                    Description = s.Description,
                    TimerMinutes = s.TimerMinutes,
                    ImageUrl = s.ImageUrl
                })
                .ToList(),

            Images = recipe.Images
                .Select(i => new ImageDto
                {
                    Id = i.Id,
                    OriginalUrl = i.OriginalUrl,
                    MediumUrl = i.MediumUrl,
                    ThumbnailUrl = i.ThumbnailUrl,
                    AltText = i.AltText,
                    IsPrimary = i.IsPrimary,
                    OrderIndex = i.OrderIndex
                })
                .ToList()
        };
    }
}
