using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.DTOs;

// Superset of the SRS response shapes ({ url, isPrimary } in FR-RCP-008 and
// { imageId, originalUrl, altText, isPrimary } in the API table) plus the Recipe's
// NEW RowVersion so the client can issue its next mutation (SRS 6.4 rule 9).
public sealed record RecipeImageDto(
    Guid ImageId,
    string Url,
    string OriginalUrl,
    string? MediumUrl,
    string? ThumbnailUrl,
    string? AltText,
    bool IsPrimary,
    int OrderIndex,
    string RowVersion)
{
    public static RecipeImageDto From(RecipeImage image, byte[] recipeRowVersion) => new(
        image.Id, image.OriginalUrl, image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl,
        image.AltText, image.IsPrimary, image.OrderIndex, RecipeRowVersion.Encode(recipeRowVersion));
}

public sealed record RecipeImageDeleteResult(string RowVersion);
