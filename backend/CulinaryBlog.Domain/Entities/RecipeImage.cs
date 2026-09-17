using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeImage : BaseEntity
{
    public Guid RecipeId { get; set; }

    public string OriginalUrl { get; set; } = string.Empty;

    public string? MediumUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? AltText { get; set; }

    public bool IsPrimary { get; set; }

    public int OrderIndex { get; set; }

    public Recipe Recipe { get; set; } = null!;
}