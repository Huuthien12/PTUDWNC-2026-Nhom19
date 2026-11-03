using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.BackgroundJobs;
using CulinaryBlog.Infrastructure.Repositories;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeImageApiTests
{
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46];
    private const int MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxMultipartBytes = MaxFileBytes + 64 * 1024;

    // ---------- helpers ----------

    private static string ObjectUrl(Guid recipeId, int n) =>
        $"https://storage.test/culinary-blog/recipes/{recipeId:D}/{n:D32}.png";

    private static string UploadObjectKey(Guid recipeId, Guid uploadId, byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return $"recipes/{recipeId:D}/{uploadId:N}-{hash}.png";
    }

    private static string Rv(Recipe recipe) => Convert.ToBase64String(recipe.RowVersion);

    private static async Task<(Recipe Recipe, List<RecipeImage> Images)> SeedAsync(
        RecipeApiFactory factory, int imageCount = 0, int primaryIndex = 0, string? urlOverride = null)
    {
        await factory.InitializeAsync();
        await using var db = factory.Database.NewContext();
        var recipe = await db.Recipes.SingleAsync();
        var images = new List<RecipeImage>();
        for (var i = 0; i < imageCount; i++)
        {
            var image = new RecipeImage
            {
                RecipeId = recipe.Id,
                OriginalUrl = urlOverride ?? ObjectUrl(recipe.Id, i),
                OrderIndex = i,
                IsPrimary = i == primaryIndex
            };
            images.Add(image);
            db.RecipeImages.Add(image);
        }

        await db.SaveChangesAsync();
        return (recipe, images);
    }

    private static async Task<List<RecipeImage>> LoadImagesAsync(RecipeApiFactory factory)
    {
        await using var db = factory.Database.NewContext();
        return await db.RecipeImages.IgnoreQueryFilters().OrderBy(image => image.OrderIndex).ToListAsync();
    }

    private static async Task<string> CurrentRowVersionAsync(RecipeApiFactory factory, Guid recipeId)
    {
        await using var db = factory.Database.NewContext();
        return Convert.ToBase64String((await db.Recipes.SingleAsync(recipe => recipe.Id == recipeId)).RowVersion);
    }

    private static HttpRequestMessage UploadRequest(Guid recipeId, byte[]? bytes, string contentType,
        string? rowVersion, string? ifMatch = null, string? altText = null,
        Guid? uploadId = null, bool includeUploadId = true)
    {
        var form = new MultipartFormDataContent();
        if (includeUploadId)
            form.Add(new StringContent((uploadId ?? Guid.NewGuid()).ToString()), "uploadId");
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", "photo.png");
        }

        if (rowVersion is not null) form.Add(new StringContent(rowVersion), "rowVersion");
        if (altText is not null) form.Add(new StringContent(altText), "altText");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipes/{recipeId}/images") { Content = form };
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return request;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string url, string? rowVersion,
        string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (rowVersion is not null) request.Content = JsonContent.Create(new { rowVersion });
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return request;
    }

    private static HttpRequestMessage PrimaryRequest(Guid recipeId, Guid imageId, string? rowVersion,
        string? ifMatch = null) =>
        JsonRequest(HttpMethod.Patch, $"/api/v1/recipes/{recipeId}/images/{imageId}/primary", rowVersion, ifMatch);

    private static HttpRequestMessage DeleteRequest(Guid recipeId, Guid imageId, string? rowVersion,
        string? ifMatch = null) =>
        JsonRequest(HttpMethod.Delete, $"/api/v1/recipes/{recipeId}/images/{imageId}", rowVersion, ifMatch);

    private static async Task AssertProblem(HttpResponseMessage response, int status, string type)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(type, (string?)body["type"]);
        Assert.Equal(status, (int?)body["status"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)body["traceId"]));
    }

    // ---------- UPLOAD ----------

    [Fact]
    public async Task Owner_uploads_valid_image_first_image_is_primary_and_rowversion_advances()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        var response = await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe), altText: "Pho bo"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.True(dto.IsPrimary);
        Assert.Equal("Pho bo", dto.AltText);
        Assert.Equal(dto.Url, dto.OriginalUrl);
        Assert.NotEqual(Rv(recipe), dto.RowVersion);

        var call = Assert.Single(factory.Storage.StoreCalls);
        Assert.Equal(recipe.Id, call.RecipeId);
        Assert.Equal("image/png", call.ContentType);
        Assert.Equal(Png.Length, call.Length);

        var stored = Assert.Single(await LoadImagesAsync(factory));
        Assert.Equal(dto.ImageId, stored.Id);
        Assert.Equal(recipe.Id, stored.RecipeId);
        Assert.Contains(call.ObjectKey, stored.OriginalUrl);
        Assert.True(stored.IsPrimary);
        Assert.Equal(0, stored.OrderIndex);
        Assert.Equal(dto.RowVersion, await CurrentRowVersionAsync(factory, recipe.Id));
        Assert.Equal($"\"{dto.RowVersion}\"", response.Headers.GetValues("ETag").Single());
        var processing = Assert.Single(factory.ImageProcessing.Jobs);
        Assert.Equal(recipe.Id, processing.RecipeId);
        Assert.Equal(dto.ImageId, processing.ImageId);
        Assert.Equal(call.ObjectKey, processing.OriginalObjectKey);
        Assert.Empty(factory.ImageCleanup.Keys);
    }

    [Fact]
    public async Task Second_upload_is_not_primary_and_uses_the_returned_rowversion()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var first = (await (await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe))))
            .Content.ReadFromJsonAsync<RecipeImageDto>())!;

        var response = await client.SendAsync(UploadRequest(recipe.Id, Jpeg, "image/jpeg", first.RowVersion));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var second = (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.False(second.IsPrimary);
        Assert.Equal(1, second.OrderIndex);
        var rows = await LoadImagesAsync(factory);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, image => image.IsPrimary);
    }

    [Fact]
    public async Task Background_image_job_persists_medium_and_thumbnail_urls()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var response = await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe)));
        var dto = (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        var queued = Assert.Single(factory.ImageProcessing.Jobs);

        using (var scope = factory.Services.CreateScope())
        {
            var job = ActivatorUtilities.CreateInstance<RecipeImageProcessingJob>(scope.ServiceProvider);
            await job.ProcessAsync(queued.RecipeId, queued.ImageId, queued.OriginalObjectKey);
        }

        var saved = Assert.Single(await LoadImagesAsync(factory));
        Assert.EndsWith("-medium.jpg", new Uri(saved.MediumUrl!).AbsolutePath);
        Assert.EndsWith("-thumbnail.jpg", new Uri(saved.ThumbnailUrl!).AbsolutePath);
        Assert.Equal(dto.ImageId, saved.Id);
        Assert.NotEqual(dto.RowVersion, await CurrentRowVersionAsync(factory, recipe.Id));

        using (var scope = factory.Services.CreateScope())
        {
            var job = ActivatorUtilities.CreateInstance<RecipeImageProcessingJob>(scope.ServiceProvider);
            await job.ProcessAsync(queued.RecipeId, queued.ImageId, queued.OriginalObjectKey);
        }
        Assert.Single(factory.Storage.VariantSources);
        Assert.Empty(factory.Storage.DeletedKeys);
    }

    [Fact]
    public async Task Processing_deleted_image_does_not_read_original_or_generate_variants()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<RecipeImageProcessingJob>(scope.ServiceProvider);
        await job.ProcessAsync(recipe.Id, Guid.NewGuid(), $"recipes/{recipe.Id:D}/deleted.png");
        Assert.Empty(factory.Storage.VariantSources);
    }

    [Fact]
    public async Task Processing_conflict_cleans_only_its_own_variants_and_preserves_newer_recipe()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1);
        var image = images[0];
        factory.Storage.AfterVariants = async () =>
        {
            await using var db = factory.Database.NewContext();
            var current = await db.Recipes.SingleAsync();
            current.Title = "Concurrent edit";
            await db.SaveChangesAsync();
        };
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<RecipeImageProcessingJob>(scope.ServiceProvider);
        var key = $"recipes/{recipe.Id:D}/{0:D32}.png";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => job.ProcessAsync(recipe.Id, image.Id, key));
        Assert.Equal(2, factory.Storage.DeletedKeys.Count);
        Assert.DoesNotContain(key, factory.Storage.DeletedKeys);
        Assert.Null(Assert.Single(await LoadImagesAsync(factory)).MediumUrl);
        await using var verify = factory.Database.NewContext();
        Assert.Equal("Concurrent edit", (await verify.Recipes.SingleAsync()).Title);
    }

    [Fact]
    public async Task Competing_processing_attempt_cannot_delete_committed_variants()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1);
        var key = $"recipes/{recipe.Id:D}/{0:D32}.png";
        factory.Storage.AfterVariants = async () =>
        {
            factory.Storage.AfterVariants = null;
            using var competing = factory.Services.CreateScope();
            var winner = ActivatorUtilities.CreateInstance<RecipeImageProcessingJob>(competing.ServiceProvider);
            await winner.ProcessAsync(recipe.Id, images[0].Id, key);
        };
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<RecipeImageProcessingJob>(scope.ServiceProvider);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => job.ProcessAsync(recipe.Id, images[0].Id, key));
        var saved = Assert.Single(await LoadImagesAsync(factory));
        Assert.NotNull(saved.MediumUrl);
        Assert.NotNull(saved.ThumbnailUrl);
        Assert.Equal(2, factory.Storage.DeletedKeys.Count);
        Assert.All(factory.Storage.DeletedKeys, deleted =>
        {
            Assert.DoesNotContain(deleted, saved.MediumUrl);
            Assert.DoesNotContain(deleted, saved.ThumbnailUrl);
        });
    }

    [Fact]
    public async Task Queue_failure_after_upload_commit_keeps_201_and_invalidates_cache()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        factory.ImageProcessing.Unavailable = true;
        using var client = factory.Client();
        var response = await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(await LoadImagesAsync(factory));
        Assert.NotEmpty(factory.Cache.RemovedKeys);
        Assert.Empty(factory.ImageCleanup.Keys);
    }

    [Fact]
    public async Task Orphan_sweep_keeps_references_and_retries_when_any_delete_fails()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory, imageCount: 1);
        var referenced = $"recipes/{recipe.Id:D}/{0:D32}.png";
        var old = DateTime.UtcNow.AddDays(-2);
        await using (var db = factory.Database.NewContext())
        {
            var image = await db.RecipeImages.SingleAsync();
            image.CreatedAt = old;
            await db.SaveChangesAsync();
        }
        factory.Storage.Objects.AddRange([
            new(referenced, old), new($"recipes/{recipe.Id:D}/failed-delete.jpg", old),
            new($"recipes/{recipe.Id:D}/orphan.jpg", old),
            new($"recipes/{recipe.Id:D}/recent.jpg", DateTime.UtcNow), new("elsewhere/old.jpg", old)
        ]);
        factory.Storage.DeleteFailures.Add($"recipes/{recipe.Id:D}/failed-delete.jpg");
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);
        await Assert.ThrowsAsync<AggregateException>(() => job.RunAsync());
        Assert.Equal($"recipes/{recipe.Id:D}/orphan.jpg", Assert.Single(factory.Storage.DeletedKeys));
        Assert.Empty(factory.ImageProcessing.Jobs);
    }

    [Fact]
    public async Task Orphan_cleanup_waits_for_upload_commit_and_rechecks_references_under_the_shared_lock()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var uploadId = Guid.NewGuid();
        var objectKey = UploadObjectKey(recipe.Id, uploadId, Png);
        factory.Storage.Objects.Add(new StoredImageObject(objectKey, DateTime.UtcNow.AddDays(-2)));
        var storeReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowStore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupContended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Storage.BeforeStore = async cancellationToken =>
        {
            storeReached.TrySetResult();
            await allowStore.Task.WaitAsync(cancellationToken);
        };
        factory.ImageOperationLock.Contended = id =>
        {
            if (id == recipe.Id) cleanupContended.TrySetResult();
        };

        var uploadTask = client.SendAsync(UploadRequest(
            recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId));
        await storeReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        using var scope = factory.Services.CreateScope();
        var cleanup = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);
        var cleanupTask = cleanup.RunAsync();
        await cleanupContended.Task.WaitAsync(TimeSpan.FromSeconds(10));

        allowStore.TrySetResult();
        var uploadResponse = await uploadTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        await cleanupTask.WaitAsync(TimeSpan.FromSeconds(10));

        var image = Assert.Single(await LoadImagesAsync(factory));
        Assert.Equal(objectKey, RecipeImageStorageKey.ForImage(recipe.Id, image).First());
        Assert.Contains(factory.Storage.Objects, item => item.ObjectKey == objectKey);
        Assert.DoesNotContain(objectKey, factory.Storage.DeletedKeys);
    }

    [Fact]
    public async Task Cleanup_retries_when_delete_result_cannot_be_verified()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        var objectKey = $"recipes/{recipe.Id:D}/uncertain-delete.png";
        factory.Storage.Objects.Add(new StoredImageObject(objectKey, DateTime.UtcNow.AddDays(-2)));
        factory.Storage.DeleteVerificationFailures.Add(objectKey);
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);

        await Assert.ThrowsAsync<AggregateException>(() => job.RunAsync());
        Assert.DoesNotContain(factory.Storage.Objects, item => item.ObjectKey == objectKey);

        factory.Storage.DeleteVerificationFailures.Remove(objectKey);
        var objectCleanup = scope.ServiceProvider.GetRequiredService<IRecipeImageObjectCleanup>();
        Assert.True(await objectCleanup.DeleteIfUnreferencedAsync(objectKey));
    }

    [Fact]
    public async Task Cancelled_upload_releases_the_recipe_operation_lock_without_storing()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        using var cancellation = new CancellationTokenSource();
        var storeReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Storage.BeforeStore = async cancellationToken =>
        {
            storeReached.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        var upload = client.SendAsync(
            UploadRequest(recipe.Id, Png, "image/png", Rv(recipe)),
            cancellation.Token);
        await storeReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);

        using var reacquireTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reacquired = await factory.ImageOperationLock.AcquireAsync(
            recipe.Id,
            reacquireTimeout.Token);
        Assert.Empty(factory.Storage.StoreCalls);
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Cleanup_accepts_a_lost_delete_response_only_after_fake_storage_confirms_absence()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        var objectKey = $"recipes/{recipe.Id:D}/lost-delete-response.png";
        factory.Storage.Objects.Add(new StoredImageObject(objectKey, DateTime.UtcNow.AddDays(-2)));
        factory.Storage.DeleteFailuresAfterEffect.Add(objectKey);
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);

        await job.RunAsync();

        Assert.DoesNotContain(factory.Storage.Objects, item => item.ObjectKey == objectKey);
        Assert.Contains(objectKey, factory.Storage.DeletedKeys);
    }

    [Fact]
    public async Task Pending_image_recovery_does_not_wait_for_the_orphan_grace_period()
    {
        await using var factory = new RecipeApiFactory();
        var (_, images) = await SeedAsync(factory, imageCount: 1);
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);

        await job.RecoverPendingAsync();

        Assert.Equal(images[0].Id, Assert.Single(factory.ImageProcessing.Jobs).ImageId);
    }

    [Fact]
    public async Task Pending_image_recovery_throws_for_retry_and_remains_idempotent()
    {
        await using var factory = new RecipeApiFactory();
        var (_, images) = await SeedAsync(factory, imageCount: 1);
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);
        factory.ImageProcessing.Unavailable = true;

        await Assert.ThrowsAsync<AggregateException>(() => job.RecoverPendingAsync());
        factory.ImageProcessing.Unavailable = false;
        await job.RecoverPendingAsync();

        Assert.Equal(images[0].Id, Assert.Single(factory.ImageProcessing.Jobs).ImageId);
    }

    [Fact]
    public async Task Orphan_sweep_pages_all_references_and_keeps_soft_deleted_rows()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        var old = DateTime.UtcNow.AddDays(-2);
        await using (var db = factory.Database.NewContext())
        {
            for (var index = 0; index < 205; index++)
            {
                var url = ObjectUrl(recipe.Id, index);
                db.RecipeImages.Add(new RecipeImage
                {
                    RecipeId = recipe.Id,
                    OriginalUrl = url,
                    OrderIndex = index,
                    IsDeleted = index == 204
                });
                factory.Storage.Objects.Add(new($"recipes/{recipe.Id:D}/{index:D32}.png", old));
            }
            await db.SaveChangesAsync();
        }
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);

        await job.RunAsync();

        Assert.Empty(factory.Storage.DeletedKeys);
    }

    [Fact]
    public void Orphan_jobs_prevent_concurrent_execution()
    {
        var methods = new[]
        {
            nameof(OrphanImageCleanupJob.RunAsync),
            nameof(OrphanImageCleanupJob.RecoverPendingAsync)
        };
        Assert.All(methods, method => Assert.NotNull(typeof(OrphanImageCleanupJob).GetMethod(method)!
            .GetCustomAttributes(typeof(DisableConcurrentExecutionAttribute), inherit: true).SingleOrDefault()));
        Assert.All(methods, method => Assert.NotNull(typeof(OrphanImageCleanupJob).GetMethod(method)!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: true).SingleOrDefault()));
    }

    [Fact]
    public async Task Deleting_image_queues_original_and_generated_variants_for_cleanup()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1);
        var image = images[0];
        image.MediumUrl = ObjectUrl(recipe.Id, 0).Replace(".png", "-medium.jpg");
        image.ThumbnailUrl = ObjectUrl(recipe.Id, 0).Replace(".png", "-thumbnail.jpg");
        await using (var db = factory.Database.NewContext())
        {
            var stored = await db.RecipeImages.SingleAsync(item => item.Id == image.Id);
            stored.MediumUrl = image.MediumUrl;
            stored.ThumbnailUrl = image.ThumbnailUrl;
            await db.SaveChangesAsync();
        }

        using var client = factory.Client();
        var response = await client.SendAsync(DeleteRequest(recipe.Id, image.Id, await CurrentRowVersionAsync(factory, recipe.Id)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(3, factory.ImageCleanup.Keys.Count);
        Assert.Contains(factory.ImageCleanup.Keys, key => key.EndsWith("-medium.jpg", StringComparison.Ordinal));
        Assert.Contains(factory.ImageCleanup.Keys, key => key.EndsWith("-thumbnail.jpg", StringComparison.Ordinal));
        Assert.Contains(factory.ImageCleanup.Keys, key => key.EndsWith(".png", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Upload_accepts_rowversion_from_if_match_header()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        var response = await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", null, ifMatch: $"\"{Rv(recipe)}\""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_without_rowversion_is_400_validation_error()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", null)), 400, "VALIDATION_ERROR");
        Assert.Empty(factory.Storage.StoreCalls);
    }

    [Fact]
    public async Task Upload_without_file_is_400_validation_error()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, null, "image/png", Rv(recipe))), 400, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Guest_upload_is_rfc7807_401()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client(role: null);

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe))), 401, "AUTH_TOKEN_INVALID");
    }

    [Fact]
    public async Task Other_author_cannot_upload_and_storage_is_untouched()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client(userId: "other-author");

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe))), 403, "RECIPE_FORBIDDEN");
        Assert.Empty(factory.Storage.StoreCalls);
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Admin_can_upload_to_any_recipe()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var admin = factory.Client("Admin", userId: "admin-user");

        var response = await admin.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_to_missing_recipe_is_404()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(UploadRequest(Guid.NewGuid(), Png, "image/png", Rv(recipe))), 404, "RECIPE_NOT_FOUND");
    }

    [Fact]
    public async Task Upload_with_wrong_signature_for_declared_mime_is_400_and_nothing_is_saved()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        // PNG bytes declared as JPEG.
        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/jpeg", Rv(recipe))), 400, "INVALID_IMAGE_FILE");
        // Not an image at all.
        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Encoding.UTF8.GetBytes("hello"), "text/plain", Rv(recipe))), 400, "INVALID_IMAGE_FILE");
        Assert.Empty(await LoadImagesAsync(factory));
        Assert.Equal(Rv(recipe), await CurrentRowVersionAsync(factory, recipe.Id));
    }

    [Fact]
    public async Task Upload_larger_than_5_mib_is_400()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var oversized = new byte[MaxFileBytes + 1];
        Png.CopyTo(oversized, 0);

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, oversized, "image/png", Rv(recipe))), 400, "INVALID_IMAGE_FILE");
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Multipart_body_larger_than_envelope_is_rejected_before_storage()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var oversized = new byte[MaxMultipartBytes + 1];
        Png.CopyTo(oversized, 0);

        var response = await client.SendAsync(UploadRequest(recipe.Id, oversized, "image/png", Rv(recipe)));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertProblem(response, 400, "VALIDATION_ERROR");
        Assert.Empty(factory.Storage.StoreCalls);
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Upload_with_stale_rowversion_is_422_before_anything_is_stored()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        var stale = Rv(recipe);
        await using (var db = factory.Database.NewContext())
        {
            var current = await db.Recipes.SingleAsync();
            current.Description = "Edited elsewhere";
            await db.SaveChangesAsync();
        }

        using var client = factory.Client();
        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", stale)), 422, "RECIPE_CONCURRENCY_CONFLICT");
        Assert.Empty(factory.Storage.StoreCalls);
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Upload_losing_a_commit_race_is_422_and_the_grace_sweep_cleans_the_orphan()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        // Another request changes the recipe after the file was stored but before the DB commit.
        factory.Storage.AfterStore = async () =>
        {
            await using var db = factory.Database.NewContext();
            var current = await db.Recipes.SingleAsync();
            current.Description = "Edited during upload";
            await db.SaveChangesAsync();
        };
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe))), 422, "RECIPE_CONCURRENCY_CONFLICT");

        Assert.Empty(await LoadImagesAsync(factory));
        Assert.Empty(factory.ImageCleanup.Keys);
        var storedObject = Assert.Single(factory.Storage.Objects);
        factory.Storage.Objects[0] = storedObject with { LastModifiedUtc = DateTime.UtcNow.AddDays(-2) };
        using var scope = factory.Services.CreateScope();
        var cleanup = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);
        await cleanup.RunAsync();
        Assert.Equal(storedObject.ObjectKey, Assert.Single(factory.Storage.DeletedKeys));
    }

    [Fact]
    public async Task Upload_when_storage_is_down_is_503_and_creates_no_row()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        factory.Storage.Unavailable = true;
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe))), 503, "MINIO_UNAVAILABLE");
        Assert.Empty(await LoadImagesAsync(factory));
        Assert.Empty(factory.ImageCleanup.Keys);
    }

    [Fact]
    public async Task Upload_requires_a_retry_stable_upload_id_before_storage()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        await AssertProblem(
            await client.SendAsync(UploadRequest(
                recipe.Id, Png, "image/png", Rv(recipe), includeUploadId: false)),
            400,
            "VALIDATION_ERROR");

        Assert.Empty(factory.Storage.StoreCalls);
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Upload_retry_after_storage_write_error_reuses_image_id_without_duplicate_rows()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var uploadId = Guid.NewGuid();
        var failAfterStore = true;
        factory.Storage.AfterStore = () =>
        {
            if (!failAfterStore) return Task.CompletedTask;
            failAfterStore = false;
            throw new ServiceUnavailableException("MINIO_UNAVAILABLE", "Image storage is unavailable.");
        };

        await AssertProblem(
            await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId)),
            503,
            "MINIO_UNAVAILABLE");
        Assert.Empty(await LoadImagesAsync(factory));

        var retry = await client.SendAsync(UploadRequest(
            recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId));

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var result = (await retry.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.Equal(uploadId, result.ImageId);
        Assert.Equal(2, factory.Storage.StoreCalls.Count);
        Assert.Equal(factory.Storage.StoreCalls[0].ObjectKey, factory.Storage.StoreCalls[1].ObjectKey);
        Assert.Single(await LoadImagesAsync(factory));
        var storedObject = Assert.Single(factory.Storage.Objects);
        factory.Storage.Objects[0] = storedObject with { LastModifiedUtc = DateTime.UtcNow.AddDays(-2) };
        using var scope = factory.Services.CreateScope();
        var cleanup = ActivatorUtilities.CreateInstance<OrphanImageCleanupJob>(scope.ServiceProvider);
        await cleanup.RunAsync();
        Assert.Empty(factory.Storage.DeletedKeys);
    }

    [Fact]
    public async Task Upload_replay_after_commit_returns_existing_image_without_storing_again()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var uploadId = Guid.NewGuid();

        var firstResponse = await client.SendAsync(UploadRequest(
            recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<RecipeImageDto>())!;

        var replay = await client.SendAsync(UploadRequest(
            recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId));

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayed = (await replay.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.Equal(first.ImageId, replayed.ImageId);
        Assert.Equal(first.OriginalUrl, replayed.OriginalUrl);
        Assert.Equal(first.RowVersion, replayed.RowVersion);
        Assert.Single(factory.Storage.StoreCalls);
        Assert.Single(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Upload_reusing_upload_id_with_different_content_or_alt_text_is_conflict_without_mutation()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();
        var uploadId = Guid.NewGuid();

        var firstResponse = await client.SendAsync(UploadRequest(
            recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId, altText: "Original"));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        var originalObject = Assert.Single(factory.Storage.Objects);

        await AssertProblem(
            await client.SendAsync(UploadRequest(
                recipe.Id, Jpeg, "image/jpeg", first.RowVersion, uploadId: uploadId, altText: "Original")),
            409,
            "UPLOAD_ID_CONFLICT");
        await AssertProblem(
            await client.SendAsync(UploadRequest(
                recipe.Id, Png, "image/png", first.RowVersion, uploadId: uploadId, altText: "Changed")),
            409,
            "UPLOAD_ID_CONFLICT");

        var persisted = Assert.Single(await LoadImagesAsync(factory));
        Assert.Equal(first.OriginalUrl, persisted.OriginalUrl);
        Assert.Equal("Original", persisted.AltText);
        Assert.Equal(first.RowVersion, await CurrentRowVersionAsync(factory, recipe.Id));
        Assert.Equal(originalObject, Assert.Single(factory.Storage.Objects));
        Assert.Single(factory.Storage.StoreCalls);
    }

    [Fact]
    public async Task Concurrent_uploads_with_same_upload_id_and_different_content_accept_only_one_payload()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var firstClient = factory.Client();
        using var secondClient = factory.Client();
        var uploadId = Guid.NewGuid();
        var firstAtStorage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstStore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRequestWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Storage.BeforeStore = async cancellationToken =>
        {
            firstAtStorage.TrySetResult();
            await allowFirstStore.Task.WaitAsync(cancellationToken);
        };
        factory.ImageOperationLock.Contended = id =>
        {
            if (id == recipe.Id) secondRequestWaiting.TrySetResult();
        };

        var firstTask = firstClient.SendAsync(UploadRequest(
            recipe.Id, Png, "image/png", Rv(recipe), uploadId: uploadId));
        await firstAtStorage.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondTask = secondClient.SendAsync(UploadRequest(
            recipe.Id, Jpeg, "image/jpeg", Rv(recipe), uploadId: uploadId));
        await secondRequestWaiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        allowFirstStore.TrySetResult();
        var responses = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await AssertProblem(conflict, 409, "UPLOAD_ID_CONFLICT");
        Assert.Single(await LoadImagesAsync(factory));
        Assert.Single(factory.Storage.Objects);
        Assert.Single(factory.Storage.StoreCalls);
    }

    // ---------- SET PRIMARY ----------

    [Fact]
    public async Task Owner_sets_primary_and_previous_primary_is_cleared()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 3, primaryIndex: 0);
        using var client = factory.Client();

        var response = await client.SendAsync(PrimaryRequest(recipe.Id, images[2].Id, Rv(recipe)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.Equal(images[2].Id, dto.ImageId);
        Assert.True(dto.IsPrimary);
        Assert.NotEqual(Rv(recipe), dto.RowVersion);
        Assert.Equal(dto.RowVersion, await CurrentRowVersionAsync(factory, recipe.Id));
        Assert.Equal($"\"{dto.RowVersion}\"", response.Headers.GetValues("ETag").Single());
        var primary = Assert.Single(await LoadImagesAsync(factory), image => image.IsPrimary);
        Assert.Equal(images[2].Id, primary.Id);
    }

    [Fact]
    public async Task Setting_the_current_primary_again_is_ok_and_still_advances_rowversion()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 2, primaryIndex: 0);
        using var client = factory.Client();

        var response = await client.SendAsync(PrimaryRequest(recipe.Id, images[0].Id, null, ifMatch: $"\"{Rv(recipe)}\""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        Assert.NotEqual(Rv(recipe), dto.RowVersion);
        Assert.Equal(images[0].Id, Assert.Single(await LoadImagesAsync(factory), image => image.IsPrimary).Id);
    }

    [Fact]
    public async Task Image_of_another_recipe_cannot_be_made_primary()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1);
        Guid foreignImageId;
        await using (var db = factory.Database.NewContext())
        {
            var other = new Recipe { Title = "Other", Slug = "other-recipe", AuthorId = recipe.AuthorId, CategoryId = recipe.CategoryId };
            var foreign = new RecipeImage { RecipeId = other.Id, OriginalUrl = ObjectUrl(other.Id, 7), OrderIndex = 0, IsPrimary = false };
            db.Recipes.Add(other);
            db.RecipeImages.Add(foreign);
            await db.SaveChangesAsync();
            foreignImageId = foreign.Id;
        }

        using var client = factory.Client();
        await AssertProblem(await client.SendAsync(PrimaryRequest(recipe.Id, foreignImageId, Rv(recipe))), 404, "RECIPE_IMAGE_NOT_FOUND");

        await using var check = factory.Database.NewContext();
        Assert.False((await check.RecipeImages.IgnoreQueryFilters().SingleAsync(image => image.Id == foreignImageId)).IsPrimary);
        Assert.True((await check.RecipeImages.IgnoreQueryFilters().SingleAsync(image => image.Id == images[0].Id)).IsPrimary);
    }

    [Fact]
    public async Task Set_primary_other_author_is_403_guest_is_401_missing_recipe_is_404()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 2);
        using var other = factory.Client(userId: "other-author");
        using var guest = factory.Client(role: null);
        using var owner = factory.Client();

        await AssertProblem(await other.SendAsync(PrimaryRequest(recipe.Id, images[1].Id, Rv(recipe))), 403, "RECIPE_FORBIDDEN");
        await AssertProblem(await guest.SendAsync(PrimaryRequest(recipe.Id, images[1].Id, Rv(recipe))), 401, "AUTH_TOKEN_INVALID");
        await AssertProblem(await owner.SendAsync(PrimaryRequest(Guid.NewGuid(), images[1].Id, Rv(recipe))), 404, "RECIPE_NOT_FOUND");
        Assert.Equal(images[0].Id, Assert.Single(await LoadImagesAsync(factory), image => image.IsPrimary).Id);
    }

    [Fact]
    public async Task Set_primary_with_stale_rowversion_is_422_and_changes_nothing()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 2, primaryIndex: 0);
        var stale = Rv(recipe);
        await using (var db = factory.Database.NewContext())
        {
            var current = await db.Recipes.SingleAsync();
            current.Description = "Edited elsewhere";
            await db.SaveChangesAsync();
        }

        using var client = factory.Client();
        await AssertProblem(await client.SendAsync(PrimaryRequest(recipe.Id, images[1].Id, stale)), 422, "RECIPE_CONCURRENCY_CONFLICT");
        Assert.Equal(images[0].Id, Assert.Single(await LoadImagesAsync(factory), image => image.IsPrimary).Id);
    }

    [Fact]
    public async Task Set_primary_without_rowversion_is_400()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 2);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(PrimaryRequest(recipe.Id, images[1].Id, null)), 400, "VALIDATION_ERROR");
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Owner_deletes_non_primary_image_row_is_removed_and_object_cleanup_is_queued()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 3, primaryIndex: 0);
        using var client = factory.Client();

        var response = await client.SendAsync(DeleteRequest(recipe.Id, images[1].Id, Rv(recipe)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var newVersion = await CurrentRowVersionAsync(factory, recipe.Id);
        Assert.NotEqual(Rv(recipe), newVersion);
        Assert.Equal($"\"{newVersion}\"", response.Headers.GetValues("ETag").Single());
        var rows = await LoadImagesAsync(factory);
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, image => image.Id == images[1].Id);
        Assert.Equal(images[0].Id, Assert.Single(rows, image => image.IsPrimary).Id);
        // Object deletion is asynchronous (Hangfire job -> IFileStorageService.DeleteAsync), never inline.
        Assert.Equal($"recipes/{recipe.Id:D}/{1:D32}.png", Assert.Single(factory.ImageCleanup.Keys));
        Assert.Empty(factory.Storage.DeletedKeys);
    }

    [Fact]
    public async Task Deleting_the_primary_promotes_the_first_remaining_image()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 3, primaryIndex: 0);
        using var client = factory.Client();

        var response = await client.SendAsync(DeleteRequest(recipe.Id, images[0].Id, null, ifMatch: $"\"{Rv(recipe)}\""));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rows = await LoadImagesAsync(factory);
        Assert.Equal(2, rows.Count);
        Assert.Equal(images[1].Id, Assert.Single(rows, image => image.IsPrimary).Id);
    }

    [Fact]
    public async Task Deleting_the_only_image_leaves_the_recipe_without_images()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1);
        using var client = factory.Client();

        var response = await client.SendAsync(DeleteRequest(recipe.Id, images[0].Id, Rv(recipe)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await LoadImagesAsync(factory));
    }

    [Fact]
    public async Task Deleting_an_image_that_is_not_in_object_storage_queues_nothing()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1, urlOverride: "https://example.test/external.jpg");
        using var client = factory.Client();

        var response = await client.SendAsync(DeleteRequest(recipe.Id, images[0].Id, Rv(recipe)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(factory.ImageCleanup.Keys);
    }

    [Fact]
    public async Task Delete_other_author_is_403_guest_is_401_and_admin_is_allowed()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 2);
        using var other = factory.Client(userId: "other-author");
        using var guest = factory.Client(role: null);
        using var admin = factory.Client("Admin", userId: "admin-user");

        await AssertProblem(await other.SendAsync(DeleteRequest(recipe.Id, images[1].Id, Rv(recipe))), 403, "RECIPE_FORBIDDEN");
        await AssertProblem(await guest.SendAsync(DeleteRequest(recipe.Id, images[1].Id, Rv(recipe))), 401, "AUTH_TOKEN_INVALID");
        Assert.Equal(2, (await LoadImagesAsync(factory)).Count);
        Assert.Empty(factory.ImageCleanup.Keys);

        var response = await admin.SendAsync(DeleteRequest(recipe.Id, images[1].Id, Rv(recipe)));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_missing_image_or_recipe_is_404()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory, imageCount: 1);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(DeleteRequest(recipe.Id, Guid.NewGuid(), Rv(recipe))), 404, "RECIPE_IMAGE_NOT_FOUND");
        await AssertProblem(await client.SendAsync(DeleteRequest(Guid.NewGuid(), Guid.NewGuid(), Rv(recipe))), 404, "RECIPE_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_with_stale_rowversion_is_422_keeps_the_row_and_queues_nothing()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 2, primaryIndex: 0);
        var stale = Rv(recipe);
        await using (var db = factory.Database.NewContext())
        {
            var current = await db.Recipes.SingleAsync();
            current.Description = "Edited elsewhere";
            await db.SaveChangesAsync();
        }

        using var client = factory.Client();
        await AssertProblem(await client.SendAsync(DeleteRequest(recipe.Id, images[0].Id, stale)), 422, "RECIPE_CONCURRENCY_CONFLICT");
        Assert.Equal(2, (await LoadImagesAsync(factory)).Count);
        Assert.Empty(factory.ImageCleanup.Keys);
    }

    [Fact]
    public async Task Delete_without_rowversion_is_400()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, images) = await SeedAsync(factory, imageCount: 1);
        using var client = factory.Client();

        await AssertProblem(await client.SendAsync(DeleteRequest(recipe.Id, images[0].Id, null)), 400, "VALIDATION_ERROR");
        Assert.Single(await LoadImagesAsync(factory));
    }

    // ---------- CONCURRENCY CHAIN ----------

    [Fact]
    public async Task Each_image_mutation_returns_the_rowversion_needed_for_the_next_one()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        using var client = factory.Client();

        var first = (await (await client.SendAsync(UploadRequest(recipe.Id, Png, "image/png", Rv(recipe)))).Content
            .ReadFromJsonAsync<RecipeImageDto>())!;
        var second = (await (await client.SendAsync(UploadRequest(recipe.Id, Jpeg, "image/jpeg", first.RowVersion))).Content
            .ReadFromJsonAsync<RecipeImageDto>())!;
        var promoted = await client.SendAsync(PrimaryRequest(recipe.Id, second.ImageId, second.RowVersion));
        var promotedDto = (await promoted.Content.ReadFromJsonAsync<RecipeImageDto>())!;
        var deleted = await client.SendAsync(DeleteRequest(recipe.Id, first.ImageId, promotedDto.RowVersion));

        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(4, new[] { Rv(recipe), first.RowVersion, second.RowVersion, promotedDto.RowVersion }.Distinct().Count());
        var remaining = Assert.Single(await LoadImagesAsync(factory));
        Assert.Equal(second.ImageId, remaining.Id);
        Assert.True(remaining.IsPrimary);

        // Re-using an already consumed RowVersion is now stale.
        using var again = factory.Client();
        await AssertProblem(await again.SendAsync(DeleteRequest(recipe.Id, second.ImageId, promotedDto.RowVersion)), 422, "RECIPE_CONCURRENCY_CONFLICT");
    }

    // ---------- REPOSITORY / DOMAIN / INFRASTRUCTURE ----------

    [Fact]
    public async Task Transaction_rolls_back_the_first_step_when_a_later_step_fails()
    {
        await using var factory = new RecipeApiFactory();
        var (seeded, _) = await SeedAsync(factory, imageCount: 2, primaryIndex: 0);
        await using (var db = factory.Database.NewContext())
        {
            var repository = new RecipeRepository(db);
            var recipe = (await repository.GetForImagesAsync(seeded.Id))!;

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExecuteInTransactionAsync<bool>(async token =>
            {
                recipe.Images.Single(image => image.IsPrimary).IsPrimary = false;
                await db.SaveChangesAsync(token);
                throw new InvalidOperationException("second step failed");
            }));
        }

        Assert.Single(await LoadImagesAsync(factory), image => image.IsPrimary);
    }

    [Fact]
    public void Recipe_creates_images_with_primary_and_order_rules()
    {
        var recipe = new Recipe { RowVersion = [1, 2, 3] };
        var first = recipe.CreateImage("https://x.test/a.png", "  alt  ");
        recipe.Images.Add(first);
        var second = recipe.CreateImage("https://x.test/b.png", null);
        recipe.Images.Add(second);

        Assert.True(first.IsPrimary);
        Assert.Equal("alt", first.AltText);
        Assert.Equal(0, first.OrderIndex);
        Assert.False(second.IsPrimary);
        Assert.Equal(1, second.OrderIndex);
        Assert.Same(first, recipe.NextPrimaryCandidate());
        Assert.Throws<NotFoundException>(() => recipe.GetImage(Guid.NewGuid()));
        Assert.Throws<BusinessRuleException>(() => recipe.PromoteImage(second));
        Assert.True(recipe.DemoteOtherPrimaryImages(second));
        recipe.PromoteImage(second);
        Assert.True(second.IsPrimary);
        Assert.False(first.IsPrimary);
    }

    [Fact]
    public void Stale_rowversion_raises_the_concurrency_exception()
    {
        var recipe = new Recipe { RowVersion = [1, 2, 3] };

        recipe.EnsureCurrentRowVersion([1, 2, 3]);
        var error = Assert.Throws<ConcurrencyException>(() => recipe.EnsureCurrentRowVersion([9, 9, 9]));
        Assert.Equal("RECIPE_CONCURRENCY_CONFLICT", error.ErrorCode);
    }

    [Theory]
    [InlineData("http://localhost:9000/culinary-blog/recipes/{0}/abc.png", true, "recipes/{0}/abc.png")]
    [InlineData("https://cdn.test/recipes/{0}/abc.jpg?X-Amz=1", true, "recipes/{0}/abc.jpg")]
    [InlineData("https://example.test/external.jpg", false, "")]
    [InlineData("https://cdn.test/recipes/{0}/../secret.png", false, "")]
    [InlineData("https://cdn.test/recipes/{0}/", false, "")]
    [InlineData("", false, "")]
    public void Object_key_is_extracted_only_for_this_recipes_objects(string urlTemplate, bool expected, string keyTemplate)
    {
        var recipeId = Guid.NewGuid();
        var url = string.Format(urlTemplate, recipeId.ToString("D"));

        var ok = RecipeImageStorageKey.TryFromUrl(url, recipeId, out var key);

        Assert.Equal(expected, ok);
        Assert.Equal(expected ? string.Format(keyTemplate, recipeId.ToString("D")) : string.Empty, key);
        Assert.False(RecipeImageStorageKey.TryFromUrl(url, Guid.NewGuid(), out _));
    }

    [Fact]
    public async Task Cleanup_job_deletes_the_object_through_the_storage_abstraction()
    {
        await using var factory = new RecipeApiFactory();
        var (recipe, _) = await SeedAsync(factory);
        var objectKey = $"recipes/{recipe.Id:D}/orphan.png";
        factory.Storage.Objects.Add(new StoredImageObject(objectKey, DateTime.UtcNow.AddDays(-2)));
        using var scope = factory.Services.CreateScope();
        var cleanup = ActivatorUtilities.CreateInstance<ImageCleanupJob>(scope.ServiceProvider);

        await cleanup.DeleteAsync(objectKey);

        Assert.Equal(objectKey, Assert.Single(factory.Storage.DeletedKeys));
        Assert.DoesNotContain(factory.Storage.Objects, item => item.ObjectKey == objectKey);
    }
}
