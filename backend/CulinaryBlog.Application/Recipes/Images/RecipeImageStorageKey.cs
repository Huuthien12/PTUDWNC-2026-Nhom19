using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.Images;

// The DB stores public URLs; IFileStorageService.DeleteAsync needs the object key
// (FR-FILE-002: "Trich xuat object name tu URL"). Objects stay under recipes/{recipeId}/;
// current uploads use {uploadId}-{contentHash}.{ext} names and older GUID names remain valid.
public static class RecipeImageStorageKey
{
    private const int Sha256HexLength = 64;

    public static bool TryGetRecipeIdFromObjectKey(string? objectKey, out Guid recipeId)
    {
        recipeId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(objectKey) ||
            objectKey.Contains('\\') ||
            objectKey.Contains("//", StringComparison.Ordinal) ||
            objectKey.Contains("..", StringComparison.Ordinal))
            return false;

        const string prefix = "recipes/";
        if (!objectKey.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var recipeEnd = objectKey.IndexOf('/', prefix.Length);
        return recipeEnd > prefix.Length &&
            recipeEnd < objectKey.Length - 1 &&
            Guid.TryParseExact(objectKey.AsSpan(prefix.Length, recipeEnd - prefix.Length), "D", out recipeId);
    }

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

    public static bool TryGetUploadContentHash(
        string? url,
        Guid recipeId,
        Guid uploadId,
        out string contentHash)
    {
        contentHash = string.Empty;
        if (!TryFromUrl(url, recipeId, out var objectKey)) return false;

        var fileName = objectKey[(objectKey.LastIndexOf('/') + 1)..];
        var uploadPrefix = $"{uploadId:N}-";
        var extensionIndex = fileName.LastIndexOf('.');
        if (!fileName.StartsWith(uploadPrefix, StringComparison.Ordinal) ||
            extensionIndex < uploadPrefix.Length + Sha256HexLength ||
            extensionIndex != uploadPrefix.Length + Sha256HexLength)
            return false;

        var hash = fileName.AsSpan(uploadPrefix.Length, Sha256HexLength);
        foreach (var character in hash)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
                return false;
        }

        contentHash = hash.ToString();
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
