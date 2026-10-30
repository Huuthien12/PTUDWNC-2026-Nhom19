using CulinaryBlog.Application.Common.Interfaces;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using CulinaryBlog.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Minio.Exceptions;

namespace CulinaryBlog.Infrastructure.Storage;

public sealed class MinioOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Bucket { get; init; } = "culinary-blog";
    public string? PublicBaseUrl { get; init; }
    public bool UseSsl { get; init; }
}

public sealed class MinioFileStorageService(
    IOptions<MinioOptions> options,
    ImageFileValidator validator,
    RecipeImageResizer resizer,
    IImageCleanupQueue cleanup,
    ILogger<MinioFileStorageService> logger) : IFileStorageService
{
    public async Task<StoredFile> StoreAsync(Guid recipeId, Guid uploadId, Stream content, string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        var image = validator.Validate(content, declaredContentType);
        var config = options.Value;
        content.Position = 0;
        var contentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        content.Position = 0;
        // Same-file retries reuse the object key; a different payload cannot overwrite it.
        var objectKey = $"recipes/{recipeId:D}/{uploadId:N}-{contentHash}.{image.Extension}";
        var client = CreateClient(config);
        try
        {
            await EnsureBucketAsync(client, config.Bucket, cancellationToken);
            content.Position = 0;
            await client.PutObjectAsync(new PutObjectArgs().WithBucket(config.Bucket).WithObject(objectKey)
                .WithStreamData(content).WithObjectSize(image.Length).WithContentType(image.ContentType), cancellationToken);
        }
        catch (Exception) { throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable."); }
        return new(objectKey, BuildUrl(config, objectKey), image.ContentType, image.Length);
    }

    public async Task<StoredImageVariants> CreateVariantsAsync(Guid recipeId, string originalObjectKey,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"recipes/{recipeId:D}/";
        if (!originalObjectKey.StartsWith(prefix, StringComparison.Ordinal) ||
            originalObjectKey.Contains("..", StringComparison.Ordinal) ||
            originalObjectKey.Contains('\\'))
            throw new ArgumentException("The source object must belong to the specified recipe.", nameof(originalObjectKey));

        var config = options.Value;
        var client = CreateClient(config);
        var original = new MemoryStream();
        try
        {
            await client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(config.Bucket)
                .WithObject(originalObjectKey)
                .WithCallbackStream(stream => stream.CopyTo(original)), cancellationToken);

            original.Position = 0;
            // Recheck size and signature before decoding a storage object.
            var contentType = Path.GetExtension(originalObjectKey).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                _ => null
            };
            validator.Validate(original, contentType);
            var resized = resizer.Resize(original);
            // Each attempt owns its files; a failed competing job cannot delete committed variants.
            var attempt = Guid.NewGuid().ToString("N");
            var mediumKey = CreateVariantKey(originalObjectKey, $"-{attempt}-medium.jpg");
            var thumbnailKey = CreateVariantKey(originalObjectKey, $"-{attempt}-thumbnail.jpg");
            var uploadedKeys = new List<string>(2);
            try
            {
                await PutVariantAsync(client, config, mediumKey, resized.Medium, cancellationToken);
                uploadedKeys.Add(mediumKey);
                await PutVariantAsync(client, config, thumbnailKey, resized.Thumbnail, cancellationToken);
                uploadedKeys.Add(thumbnailKey);
            }
            catch (Exception processingException)
            {
                foreach (var uploadedKey in uploadedKeys)
                {
                    try
                    {
                        await DeleteAsync(uploadedKey, cancellationToken);
                    }
                    catch (Exception cleanupException)
                    {
                        logger.LogError(cleanupException, "Could not remove partial image variant {ObjectKey}.", uploadedKey);
                        try
                        {
                            cleanup.EnqueueDelete(uploadedKey);
                        }
                        catch (Exception queueException)
                        {
                            logger.LogError(queueException, "Could not queue cleanup for partial image variant {ObjectKey}.", uploadedKey);
                        }
                    }
                }

                logger.LogError(processingException, "Could not upload generated image variants for {ObjectKey}.", originalObjectKey);
                throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable.");
            }

            return new StoredImageVariants(
                new StoredImageVariant(mediumKey, BuildUrl(config, mediumKey)),
                new StoredImageVariant(thumbnailKey, BuildUrl(config, thumbnailKey)));
        }
        catch (ServiceUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not generate image variants for {ObjectKey}.", originalObjectKey);
            throw new ServiceUnavailableException("IMAGE_PROCESSING_FAILED", "Image variants could not be generated.");
        }
        finally
        {
            await original.DisposeAsync();
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var client = CreateClient(config);
        Exception? deleteException = null;
        try
        {
            await client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(config.Bucket)
                .WithObject(objectKey), cancellationToken);
        }
        catch (Exception exception)
        {
            deleteException = exception;
        }

        using var verificationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(config.Bucket)
                .WithObject(objectKey), verificationTimeout.Token);
        }
        catch (ObjectNotFoundException)
        {
            if (deleteException is not null)
                logger.LogWarning(deleteException,
                    "MinIO reported an uncertain deletion for {ObjectKey}, but a follow-up check confirmed it is absent.",
                    objectKey);
            return;
        }
        catch (Exception verificationException)
        {
            logger.LogError(deleteException ?? verificationException,
                "Could not verify deletion of image object {ObjectKey}.", objectKey);
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(
                    "Image deletion was cancelled and its result could not be verified.",
                    deleteException ?? verificationException,
                    cancellationToken);
            throw new ServiceUnavailableException(
                "IMAGE_DELETE_UNCONFIRMED",
                "Image deletion could not be verified.");
        }

        if (deleteException is not null)
        {
            logger.LogError(deleteException, "Image object {ObjectKey} remains after a failed delete request.", objectKey);
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(
                    "Image deletion was cancelled and the object remains present.",
                    deleteException,
                    cancellationToken);
            throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable.");
        }

        logger.LogError("Image object {ObjectKey} remains after MinIO acknowledged its deletion.", objectKey);
        throw new ServiceUnavailableException(
            "IMAGE_DELETE_UNCONFIRMED",
            "Image deletion could not be verified.");
    }

    public async IAsyncEnumerable<StoredImageObject> ListRecipeObjectsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var client = CreateClient(config);
        await foreach (var item in client.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(config.Bucket).WithPrefix("recipes/").WithRecursive(true), cancellationToken))
        {
            if (!item.IsDir && item.LastModifiedDateTime is { } modified)
                yield return new StoredImageObject(item.Key, modified.ToUniversalTime());
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            await CreateClient(config).ListBucketsAsync(cancellationToken);
            return true;
        }
        catch { return false; }
    }

    private static IMinioClient CreateClient(MinioOptions options) => new MinioClient()
        .WithEndpoint(options.Endpoint).WithCredentials(options.AccessKey, options.SecretKey)
        .WithSSL(options.UseSsl).Build();

    private static async Task EnsureBucketAsync(IMinioClient client, string bucket, CancellationToken cancellationToken)
    {
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
    }

    private static async Task PutVariantAsync(IMinioClient client, MinioOptions options, string objectKey,
        byte[] content, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(content.Length)
            .WithContentType("image/jpeg"), cancellationToken);
    }

    private static string CreateVariantKey(string originalObjectKey, string suffix)
    {
        var extensionIndex = originalObjectKey.LastIndexOf('.');
        var stem = extensionIndex > originalObjectKey.LastIndexOf('/')
            ? originalObjectKey[..extensionIndex]
            : originalObjectKey;
        return $"{stem}{suffix}";
    }

    private static string BuildUrl(MinioOptions options, string key) =>
        $"{(string.IsNullOrWhiteSpace(options.PublicBaseUrl) ? $"{(options.UseSsl ? "https" : "http")}://{options.Endpoint}" : options.PublicBaseUrl).TrimEnd('/')}/{options.Bucket}/{key}";
}
