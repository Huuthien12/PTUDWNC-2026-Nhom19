namespace CulinaryBlog.Domain.Entities;

public class RecipeImage
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }
}