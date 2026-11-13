using System.Security.Cryptography;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.SeedUpgradeCli;

// The caller must have already completed the read-only dry-run; this service refuses any plan
// that has not received both execution approvals.
public sealed class SeedImageImportService(AppDbContext db, IFileStorageService storage)
{
    public async Task<SeedImageImportReport> ApplyAsync(IEnumerable<ImageImportPlan> input,
        CancellationToken cancellationToken = default)
    {
        var plans = input.OrderBy(item => item.RecipeNumber).ToList();
        if (plans.Count != 100 || plans.Select(item => item.RecipeNumber).Distinct().Count() != 100 ||
            plans.Select(item => item.RecipeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 100 ||
            plans.Any(item => !item.ExecutionAuthorized))
            throw new InvalidDataException("Import requires exactly 100 distinct execution-authorized plans.");

        if (!await storage.IsAvailableAsync(cancellationToken))
            throw new InvalidOperationException("MinIO is unavailable.");

        var results = new List<SeedImageImportResult>();
        foreach (var plan in plans)
        {
            try
            {
                results.Add(await ApplyOneAsync(plan, cancellationToken));
            }
            catch (Exception exception)
            {
                results.Add(new(plan.RecipeNumber, plan.RecipeId, plan.ObjectKey, "FAILED", exception.Message, []));
                break; // Fail closed: do not advance to the next production recipe after an unexpected error.
            }
        }

        return new SeedImageImportReport(db.Database.GetDbConnection().Database, results);
    }

    private async Task<SeedImageImportResult> ApplyOneAsync(ImageImportPlan plan, CancellationToken cancellationToken)
    {
        var recipeId = Guid.Parse(plan.RecipeId);
        var uploadId = ImageImportDryRun.StableUploadId(plan.RecipeId, plan.ImageSha256);
        var existing = await db.RecipeImages.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == uploadId,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.RecipeId != recipeId || !RecipeImageStorageKey.TryFromUrl(existing.OriginalUrl, recipeId, out var key) ||
                !string.Equals(key, plan.ObjectKey, StringComparison.Ordinal))
                throw new InvalidDataException($"Existing image ID conflicts for recipe {plan.RecipeNumber}.");
            return new(plan.RecipeNumber, plan.RecipeId, plan.ObjectKey, "SKIPPED_EXISTING", null, []);
        }

        var source = Path.GetFullPath(plan.Source);
        if (!File.Exists(source) || !string.Equals(Hash(source), plan.ImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Image hash does not match for recipe {plan.RecipeNumber}.");

        var originalExisted = await ObjectMatchesAsync(plan.ObjectKey, plan.ImageSha256, cancellationToken);
        StoredFile stored;
        await using (var content = File.OpenRead(source))
        {
            stored = await storage.StoreAsync(recipeId, uploadId, content, "image/webp", cancellationToken);
        }
        if (!string.Equals(stored.ObjectKey, plan.ObjectKey, StringComparison.Ordinal))
            throw new InvalidDataException($"Storage key mismatch for recipe {plan.RecipeNumber}.");

        StoredImageVariants? variants = null;
        var compensationFailures = new List<string>();
        try
        {
            variants = await storage.CreateVariantsAsync(recipeId, stored.ObjectKey, cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var recipe = await db.Recipes.Include(item => item.Images).SingleOrDefaultAsync(item => item.Id == recipeId,
                cancellationToken) ?? throw new InvalidDataException($"Recipe {plan.RecipeNumber} no longer exists.");
            if (!string.Equals(recipe.Slug, plan.Slug, StringComparison.Ordinal))
                throw new InvalidDataException($"Recipe slug changed for recipe {plan.RecipeNumber}.");

            existing = recipe.Images.SingleOrDefault(item => item.Id == uploadId);
            if (existing is not null)
            {
                if (!RecipeImageStorageKey.TryFromUrl(existing.OriginalUrl, recipeId, out var key) ||
                    !string.Equals(key, plan.ObjectKey, StringComparison.Ordinal))
                    throw new InvalidDataException($"Concurrent image ID conflict for recipe {plan.RecipeNumber}.");
                await transaction.RollbackAsync(cancellationToken);
                await CompensateAsync(stored.ObjectKey, variants, originalExisted, compensationFailures, cancellationToken);
                return new(plan.RecipeNumber, plan.RecipeId, plan.ObjectKey, "SKIPPED_EXISTING", null, compensationFailures);
            }

            var image = recipe.CreateImage(stored.Url, recipe.Title);
            image.Id = uploadId;
            image.MediumUrl = variants.Medium.Url;
            image.ThumbnailUrl = variants.Thumbnail.Url;
            db.RecipeImages.Add(image);
            recipe.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(plan.RecipeNumber, plan.RecipeId, plan.ObjectKey, "IMPORTED", null, compensationFailures);
        }
        catch
        {
            await CompensateAsync(stored.ObjectKey, variants, originalExisted, compensationFailures, cancellationToken);
            throw;
        }
    }

    private async Task<bool> ObjectMatchesAsync(string objectKey, string expectedHash, CancellationToken cancellationToken)
    {
        var existing = await storage.ReadAsync(objectKey, cancellationToken);
        if (existing is null) return false;
        await using (existing.Content)
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(existing.Content, cancellationToken)).ToLowerInvariant();
            if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Existing object hash conflicts for {objectKey}.");
        }
        return true;
    }

    private async Task CompensateAsync(string originalKey, StoredImageVariants? variants, bool originalExisted,
        List<string> failures, CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        if (variants is not null)
        {
            keys.Add(variants.Medium.ObjectKey);
            keys.Add(variants.Thumbnail.ObjectKey);
        }
        if (!originalExisted) keys.Add(originalKey);
        foreach (var key in keys)
        {
            try { await storage.DeleteAsync(key, cancellationToken); }
            catch (Exception exception) { failures.Add($"{key}: {exception.Message}"); }
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

public sealed record SeedImageImportResult(int RecipeNumber, string RecipeId, string ObjectKey, string Status,
    string? Error, List<string> CompensationFailures);

public sealed record SeedImageImportReport(string? Database, List<SeedImageImportResult> Results)
{
    public int Imported => Results.Count(item => item.Status == "IMPORTED");
    public int SkippedExisting => Results.Count(item => item.Status == "SKIPPED_EXISTING");
    public int Failed => Results.Count(item => item.Status == "FAILED");
}
