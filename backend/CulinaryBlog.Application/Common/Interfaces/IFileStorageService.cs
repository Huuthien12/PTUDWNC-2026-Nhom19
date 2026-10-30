namespace CulinaryBlog.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<StoredFile> StoreAsync(
        Guid recipeId,
        Guid uploadId,
        Stream content,
        string? declaredContentType,
        CancellationToken cancellationToken = default);

    Task<StoredImageVariants> CreateVariantsAsync(
        Guid recipeId,
        string originalObjectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredImageObject> ListRecipeObjectsAsync(CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public sealed record StoredFile(
    string ObjectKey,
    string Url,
    string ContentType,
    long Length);

public sealed record StoredImageVariant(string ObjectKey, string Url);

public sealed record StoredImageVariants(
    StoredImageVariant Medium,
    StoredImageVariant Thumbnail);

public sealed record StoredImageObject(string ObjectKey, DateTime LastModifiedUtc);
