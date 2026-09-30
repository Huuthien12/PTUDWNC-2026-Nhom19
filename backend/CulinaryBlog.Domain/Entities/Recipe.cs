using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Domain.Entities;

public class Recipe : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public int PrepTime { get; set; }

    public int CookTime { get; set; }

    public int Servings { get; set; }

    public RecipeDifficulty Difficulty { get; set; }

    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    // FK -> AspNetUsers.Id
    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public RecipeNutrition Nutrition { get; set; } = new();

    public ICollection<RecipeIngredient> Ingredients { get; set; }
        = new List<RecipeIngredient>();

    public ICollection<RecipeStep> Steps { get; set; }
        = new List<RecipeStep>();

    public ICollection<RecipeImage> Images { get; set; }
        = new List<RecipeImage>();

    public void Publish()
    {
        if (Status == RecipeStatus.Published)
        {
            return;
        }

        if (Status != RecipeStatus.Draft)
        {
            throw new BusinessRuleException(
                "RECIPE_PUBLISH_INVALID_STATE",
                "Only a draft recipe can be published.");
        }

        if (!Ingredients.Any(ingredient => !ingredient.IsDeleted) ||
            !Steps.Any(step => !step.IsDeleted))
        {
            throw new BusinessRuleException(
                "RECIPE_PUBLISH_INCOMPLETE",
                "A recipe must have at least one ingredient and one step before publishing.");
        }

        Status = RecipeStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        if (Status == RecipeStatus.Draft)
        {
            return;
        }

        if (Status != RecipeStatus.Published)
        {
            throw new BusinessRuleException(
                "RECIPE_UNPUBLISH_INVALID_STATE",
                "Only a published recipe can be unpublished.");
        }

        Status = RecipeStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    // Archive is idempotent because FR-RCP-006 specifies only the resulting Archived state.
    public void Archive()
    {
        if (Status == RecipeStatus.Archived) return;
        Status = RecipeStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }
}
