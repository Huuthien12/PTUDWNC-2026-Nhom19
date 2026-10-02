namespace CulinaryBlog.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<StoredFile> StoreAsync(
        Guid recipeId,
        Stream content,
        string? declaredContentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public sealed record StoredFile(
    string ObjectKey,
    string Url,
    string ContentType,
    long Length);
