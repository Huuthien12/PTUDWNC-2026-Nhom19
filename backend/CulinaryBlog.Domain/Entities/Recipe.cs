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

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    // ---- RecipeImage child mutations (SRS FR-RCP-008, section 6.4) ----

    // Child mutations must carry the Recipe RowVersion the client last saw (SRS 6.4 rule 3).
    public void EnsureCurrentRowVersion(byte[] clientRowVersion)
    {
        ArgumentNullException.ThrowIfNull(clientRowVersion);
        if (!RowVersion.AsSpan().SequenceEqual(clientRowVersion))
        {
            throw new ConcurrencyException(
                "RECIPE_CONCURRENCY_CONFLICT",
                "The recipe was updated by another request. Reload and try again.");
        }
    }

    public RecipeImage GetImage(Guid imageId)
        => Images.FirstOrDefault(image => image.Id == imageId && !image.IsDeleted)
            ?? throw new NotFoundException("RECIPE_IMAGE_NOT_FOUND", "Recipe image not found.");

    // Builds a NEW image (the first image of a recipe is primary automatically). It is deliberately
    // not added to Images here: the repository tracks it as Added, and EF links it to this recipe
    // through RecipeId. Adding it to the collection first would let DetectChanges treat the
    // client-generated GUID as an existing row (UPDATE instead of INSERT).
    public RecipeImage CreateImage(string originalUrl, string? altText)
    {
        var current = Images.Where(image => !image.IsDeleted).ToList();
        return new RecipeImage
        {
            RecipeId = Id,
            OriginalUrl = originalUrl,
            AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim(),
            IsPrimary = current.Count == 0,
            OrderIndex = current.Count == 0 ? 0 : current.Max(item => item.OrderIndex) + 1
        };
    }

    // Step 1 of changing the primary image: the DB allows only one primary per recipe,
    // so the old primary must be persisted as non-primary before the new one is promoted.
    public bool DemoteOtherPrimaryImages(RecipeImage keep)
    {
        var others = Images.Where(image => !image.IsDeleted && image.IsPrimary && image.Id != keep.Id).ToList();
        foreach (var other in others) other.IsPrimary = false;
        return others.Count > 0;
    }

    public void PromoteImage(RecipeImage image)
    {
        if (Images.Any(item => !item.IsDeleted && item.IsPrimary && item.Id != image.Id))
        {
            throw new BusinessRuleException(
                "RECIPE_IMAGE_PRIMARY_CONFLICT",
                "Another image is still primary; demote it first.");
        }

        image.IsPrimary = true;
    }

    public void RemoveImage(RecipeImage image) => Images.Remove(image);

    // Used after the primary image is deleted: the first remaining image takes over.
    public RecipeImage? NextPrimaryCandidate()
        => Images.Where(image => !image.IsDeleted)
            .OrderBy(image => image.OrderIndex).ThenBy(image => image.CreatedAt)
            .FirstOrDefault();
}
