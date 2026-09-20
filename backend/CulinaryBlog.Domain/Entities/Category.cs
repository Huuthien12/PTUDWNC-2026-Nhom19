using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int OrderIndex { get; set; }

    public ICollection<Recipe> Recipes { get; set; }
        = new List<Recipe>();
}
