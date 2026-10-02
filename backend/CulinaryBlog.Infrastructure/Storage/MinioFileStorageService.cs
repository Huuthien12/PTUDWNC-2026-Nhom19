using CulinaryBlog.Application.Common.Interfaces;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using CulinaryBlog.Domain.Exceptions;

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
    ImageFileValidator validator) : IFileStorageService
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

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        try { await CreateClient(config).RemoveObjectAsync(new RemoveObjectArgs().WithBucket(config.Bucket).WithObject(objectKey), cancellationToken); }
        catch (Exception) { throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable."); }
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

    private static string BuildUrl(MinioOptions options, string key) =>
        $"{(string.IsNullOrWhiteSpace(options.PublicBaseUrl) ? $"{(options.UseSsl ? "https" : "http")}://{options.Endpoint}" : options.PublicBaseUrl).TrimEnd('/')}/{options.Bucket}/{key}";
}
