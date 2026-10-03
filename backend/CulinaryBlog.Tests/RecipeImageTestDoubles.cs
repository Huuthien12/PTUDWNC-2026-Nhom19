using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Storage;

namespace CulinaryBlog.Tests;

public sealed record StoreCall(Guid RecipeId, string? ContentType, long Length, string ObjectKey);

// Real ImageFileValidator rules (size, MIME, magic bytes) without a real MinIO server.
public sealed class FakeFileStorageService : IFileStorageService
{
    private readonly ImageFileValidator _validator = new();
    public List<StoreCall> StoreCalls { get; } = [];
    public List<string> DeletedKeys { get; } = [];
    public bool Unavailable { get; set; }
    // Runs after the "upload" succeeded and before the handler writes to the database.
    public Func<Task>? AfterStore { get; set; }

    public async Task<StoredFile> StoreAsync(Guid recipeId, Stream content, string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
            throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable.");

        var image = _validator.Validate(content, declaredContentType);
        content.Position = 0;
        var key = $"recipes/{recipeId:D}/{Guid.NewGuid():N}.{image.Extension}";
        StoreCalls.Add(new StoreCall(recipeId, declaredContentType, image.Length, key));
        if (AfterStore is not null) await AfterStore();
        return new StoredFile(key, $"https://storage.test/culinary-blog/{key}", image.ContentType, image.Length);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        DeletedKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(!Unavailable);
}

public sealed class RecordingImageCleanupQueue : IImageCleanupQueue
{
    public List<string> Keys { get; } = [];
    public void EnqueueDelete(string objectKey) => Keys.Add(objectKey);
}
