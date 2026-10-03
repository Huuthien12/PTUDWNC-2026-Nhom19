using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.BackgroundJobs;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeImageApiTests
{
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46];

    // ---------- helpers ----------

    private static string ObjectUrl(Guid recipeId, int n) =>
        $"https://storage.test/culinary-blog/recipes/{recipeId:D}/{n:D32}.png";

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
        string? rowVersion, string? ifMatch = null, string? altText = null)
    {
        var form = new MultipartFormDataContent();
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
        var oversized = new byte[5 * 1024 * 1024 + 1];
        Png.CopyTo(oversized, 0);

        await AssertProblem(await client.SendAsync(UploadRequest(recipe.Id, oversized, "image/png", Rv(recipe))), 400, "INVALID_IMAGE_FILE");
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
    public async Task Upload_losing_a_commit_race_is_422_and_the_orphan_object_is_queued_for_cleanup()
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
        Assert.Equal(Assert.Single(factory.Storage.StoreCalls).ObjectKey, Assert.Single(factory.ImageCleanup.Keys));
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
        var storage = new FakeFileStorageService();

        await new ImageCleanupJob(storage).DeleteAsync("recipes/abc/def.png");

        Assert.Equal("recipes/abc/def.png", Assert.Single(storage.DeletedKeys));
    }
}
