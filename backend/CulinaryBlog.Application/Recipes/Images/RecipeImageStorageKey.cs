using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.Images;

// The DB stores public URLs; IFileStorageService.DeleteAsync needs the object key
// (FR-FILE-002: "Trich xuat object name tu URL"). Keys look like recipes/{recipeId}/{guid}.{ext}.
public static class RecipeImageStorageKey
{
    public static bool TryFromUrl(string? url, Guid recipeId, out string objectKey)
    {
        objectKey = string.Empty;
        if (string.IsNullOrWhiteSpace(url)) return false;

        var path = url.Split('?', '#')[0];
        var marker = $"recipes/{recipeId:D}/";
        var index = path.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 1 || path[index - 1] != '/') return false;

        var key = path[index..];
        if (key.Length == marker.Length ||
            key.Contains("..", StringComparison.Ordinal) ||
            key.Contains('\\') ||
            key.Contains("//", StringComparison.Ordinal))
            return false;

        objectKey = key;
        return true;
    }

    // URLs that are not objects of this recipe (for example external seed images) are skipped.
    public static IReadOnlyList<string> ForImage(Guid recipeId, RecipeImage image)
        => new[] { image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl }
            .Select(url => TryFromUrl(url, recipeId, out var key) ? key : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
