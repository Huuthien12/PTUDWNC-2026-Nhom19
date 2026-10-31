using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Storage;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace CulinaryBlog.Tests;

public sealed record StoreCall(Guid RecipeId, string? ContentType, long Length, string ObjectKey);

// Real ImageFileValidator rules (size, MIME, magic bytes) without a real MinIO server.
public sealed class FakeFileStorageService : IFileStorageService
{
    private readonly object _sync = new();
    private readonly ImageFileValidator _validator = new();
    public List<StoreCall> StoreCalls { get; } = [];
    public List<string> DeletedKeys { get; } = [];
    public List<string> VariantSources { get; } = [];
    public List<StoredImageObject> Objects { get; } = [];
    public HashSet<string> DeleteFailures { get; } = [];
    public HashSet<string> DeleteFailuresAfterEffect { get; } = [];
    public HashSet<string> DeleteVerificationFailures { get; } = [];
    public Func<Task>? AfterVariants { get; set; }
    public bool Unavailable { get; set; }
    public Func<CancellationToken, Task>? BeforeStore { get; set; }
    // Runs after the "upload" succeeded and before the handler writes to the database.
    public Func<Task>? AfterStore { get; set; }

    public async Task<StoredFile> StoreAsync(Guid recipeId, Guid uploadId, Stream content, string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
            throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable.");

        cancellationToken.ThrowIfCancellationRequested();
        if (BeforeStore is not null) await BeforeStore(cancellationToken);
        var image = _validator.Validate(content, declaredContentType);
        content.Position = 0;
        var contentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        content.Position = 0;
        var key = $"recipes/{recipeId:D}/{uploadId:N}-{contentHash}.{image.Extension}";
        lock (_sync)
        {
            StoreCalls.Add(new StoreCall(recipeId, declaredContentType, image.Length, key));
            var objectIndex = Objects.FindIndex(item => item.ObjectKey == key);
            var storedObject = new StoredImageObject(key, DateTime.UtcNow);
            if (objectIndex >= 0) Objects[objectIndex] = storedObject;
            else Objects.Add(storedObject);
        }
        if (AfterStore is not null) await AfterStore();
        return new StoredFile(key, $"https://storage.test/culinary-blog/{key}", image.ContentType, image.Length);
    }

    public async Task<StoredImageVariants> CreateVariantsAsync(Guid recipeId, string originalObjectKey,
        CancellationToken cancellationToken = default)
    {
        VariantSources.Add(originalObjectKey);
        if (AfterVariants is not null) await AfterVariants();
        var stem = Path.Combine(Path.GetDirectoryName(originalObjectKey)!, Path.GetFileNameWithoutExtension(originalObjectKey))
            .Replace('\\', '/');
        stem += $"-{Guid.NewGuid():N}";
        var mediumKey = $"{stem}-medium.jpg";
        var thumbnailKey = $"{stem}-thumbnail.jpg";
        return new StoredImageVariants(
            new StoredImageVariant(mediumKey, $"https://storage.test/culinary-blog/{mediumKey}"),
            new StoredImageVariant(thumbnailKey, $"https://storage.test/culinary-blog/{thumbnailKey}"));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (DeleteFailures.Contains(objectKey)) throw new IOException("Storage deletion failed");
        cancellationToken.ThrowIfCancellationRequested();
        var objectIndex = Objects.FindIndex(item => item.ObjectKey == objectKey);
        if (objectIndex >= 0) Objects.RemoveAt(objectIndex);
        DeletedKeys.Add(objectKey);
        if (DeleteVerificationFailures.Contains(objectKey))
            throw new IOException("Storage deletion outcome could not be verified");
        if (DeleteFailuresAfterEffect.Remove(objectKey) && Objects.Any(item => item.ObjectKey == objectKey))
            throw new IOException("Storage deletion outcome could not be verified");
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(!Unavailable);

    public async IAsyncEnumerable<StoredImageObject> ListRecipeObjectsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        foreach (var item in Objects.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}

public sealed class FakeRecipeImageOperationLock : IRecipeImageOperationLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    public Action<Guid>? Contended { get; set; }

    public async Task<IAsyncDisposable> AcquireAsync(
        Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var semaphore = _locks.GetOrAdd(recipeId, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            Contended?.Invoke(recipeId);
            await semaphore.WaitAsync(cancellationToken);
        }
        return new Lease(semaphore);
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class RecordingImageCleanupQueue : IImageCleanupQueue
{
    public List<string> Keys { get; } = [];
    public void EnqueueDelete(string objectKey) => Keys.Add(objectKey);
}

public sealed class RecordingImageProcessingQueue : IImageProcessingQueue
{
    public bool Unavailable { get; set; }
    public List<(Guid RecipeId, Guid ImageId, string OriginalObjectKey)> Jobs { get; } = [];

    public void EnqueueProcess(Guid recipeId, Guid imageId, string originalObjectKey)
    {
        if (Unavailable) throw new IOException("Processing queue unavailable");
        Jobs.Add((recipeId, imageId, originalObjectKey));
    }
}
