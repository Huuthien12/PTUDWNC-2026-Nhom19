using CulinaryBlog.Application.Common.Interfaces;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using CulinaryBlog.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

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
    public async Task<StoredFile> StoreAsync(Guid recipeId, Stream content, string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        var image = validator.Validate(content, declaredContentType);
        var config = options.Value;
        var objectKey = $"recipes/{recipeId:D}/{Guid.NewGuid():N}.{image.Extension}";
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
                        await client.RemoveObjectAsync(new RemoveObjectArgs()
                            .WithBucket(config.Bucket)
                            .WithObject(uploadedKey), cancellationToken);
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
        try { await CreateClient(config).RemoveObjectAsync(new RemoveObjectArgs().WithBucket(config.Bucket).WithObject(objectKey), cancellationToken); }
        catch (Exception) { throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable."); }
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
            return await CreateClient(config).BucketExistsAsync(new BucketExistsArgs().WithBucket(config.Bucket), cancellationToken);
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
