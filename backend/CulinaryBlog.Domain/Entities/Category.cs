using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<Recipe> Recipes { get; set; }
        = new List<Recipe>();

    public static Category Create(
        string name,
        string slug,
        string? description)
    {
        return new Category
        {
            Name = name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim()
        };
    }
}