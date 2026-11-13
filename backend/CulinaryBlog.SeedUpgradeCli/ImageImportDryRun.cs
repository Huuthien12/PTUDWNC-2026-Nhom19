using System.Security.Cryptography;
using System.Text.Json;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.SeedUpgradeCli;

public sealed class ImageImportDryRun(AppDbContext db)
{
    public async Task<ImageImportDryRunReport> AnalyzeAsync(string reviewDirectory, string? approvalPath = null,
        CancellationToken cancellationToken = default)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        approvalPath ??= Path.Combine(reviewDirectory, "image-approval-template.json");
        var approvals = await ReadAsync<ImageApprovalDocument>(approvalPath, options, cancellationToken);
        var preflight = await ReadAsync<ImagePreflightDocument>(Path.Combine(reviewDirectory, "image-preflight-report.json"), options, cancellationToken);
        if (approvals.SchemaVersion != 1 || approvals.Approvals.Count != 100 || preflight.Records.Count != 100)
            throw new InvalidDataException("Image review package must contain exactly 100 schema-version-1 records.");

        var duplicates = approvals.Approvals.GroupBy(item => new { item.RecipeNumber, item.RecipeId, item.Slug })
            .Any(group => group.Count() != 1);
        if (duplicates || approvals.Approvals.Select(item => item.RecipeNumber).Distinct().Count() != 100)
            throw new InvalidDataException("Image approval records contain duplicates.");

        var preflightByNumber = preflight.Records.ToDictionary(item => item.RecipeNumber);
        var ids = approvals.Approvals.Select(item => Guid.TryParse(item.RecipeId, out var id)
            ? id : throw new InvalidDataException($"Invalid RecipeId for recipe {item.RecipeNumber}.")).ToHashSet();
        if (ids.Count != 100) throw new InvalidDataException("Image approvals do not bind 100 distinct RecipeId values.");
        var recipes = await db.Recipes.AsNoTracking().IgnoreQueryFilters()
            .Where(recipe => ids.Contains(recipe.Id))
            .Select(recipe => new { RecipeId = recipe.Id.ToString(), recipe.Slug })
            .ToListAsync(cancellationToken);
        if (recipes.Count != 100) throw new InvalidDataException("Database does not contain every approved seed RecipeId.");
        var recipesById = recipes.ToDictionary(item => item.RecipeId, StringComparer.OrdinalIgnoreCase);

        var planned = new List<ImageImportPlan>();
        var pending = new List<ImageImportPending>();
        foreach (var approval in approvals.Approvals.OrderBy(item => item.RecipeNumber))
        {
            if (!preflightByNumber.TryGetValue(approval.RecipeNumber, out var candidate) ||
                !string.Equals(approval.RecipeId, candidate.RecipeId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(approval.Slug, candidate.Slug, StringComparison.Ordinal) ||
                !string.Equals(approval.ImageSha256, candidate.ImageSha256, StringComparison.OrdinalIgnoreCase) ||
                !recipesById.TryGetValue(approval.RecipeId, out var recipe) ||
                !string.Equals(recipe.Slug, approval.Slug, StringComparison.Ordinal))
                throw new InvalidDataException($"Approval binding mismatch for recipe {approval.RecipeNumber}.");

            if (!approval.ReviewApproved)
            {
                pending.Add(new(approval.RecipeNumber, "PENDING_VISUAL_APPROVAL"));
                continue;
            }
            if (candidate.TechnicalIssues.Count != 0)
                throw new InvalidDataException($"Approved image {approval.RecipeNumber} has not passed technical preflight.");

            var source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), candidate.Source));
            if (!File.Exists(source) || !string.Equals(Hash(source), approval.ImageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Approved image hash does not match for recipe {approval.RecipeNumber}.");
            var uploadId = StableUploadId(approval.RecipeId, approval.ImageSha256);
            planned.Add(new(approval.RecipeNumber, approval.RecipeId, approval.Slug, candidate.ImageFile,
                candidate.ImageSha256, candidate.Source,
                $"recipes/{Guid.Parse(approval.RecipeId):D}/{uploadId:N}-{approval.ImageSha256}.webp",
                approval.AutoImportAllowed && approval.ApproveImport));
            if (!(approval.AutoImportAllowed && approval.ApproveImport))
                pending.Add(new(approval.RecipeNumber, "PENDING_IMPORT_AUTHORIZATION"));
        }

        return new(db.Database.GetDbConnection().Database, planned, pending);
    }

    private static async Task<T> ReadAsync<T>(string path, JsonSerializerOptions options, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), options)
            ?? throw new InvalidDataException($"Invalid JSON: {path}");

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // Derived from immutable RecipeId + candidate hash; matches IFileStorageService's object-key convention.
    public static Guid StableUploadId(string recipeId, string imageSha256)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"seed-image-import:{recipeId}:{imageSha256}"))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}

public sealed record ImageApprovalDocument(int SchemaVersion, List<ImageApprovalRecord> Approvals);
public sealed record ImageApprovalRecord(int RecipeNumber, string RecipeId, string Slug, string ImageFile,
    string ImageSha256, bool ReviewApproved, bool AutoImportAllowed, bool ApproveImport);
public sealed record ImagePreflightDocument(List<ImagePreflightRecord> Records);
public sealed record ImagePreflightRecord(int RecipeNumber, string RecipeId, string Slug, string ImageFile, string Source,
    string ImageSha256, List<string> TechnicalIssues, string VisualReviewStatus);
public sealed record ImageImportPlan(int RecipeNumber, string RecipeId, string Slug, string ImageFile, string ImageSha256,
    string Source, string ObjectKey,
    bool ExecutionAuthorized);
public sealed record ImageImportPending(int RecipeNumber, string Reason);
public sealed record ImageImportDryRunReport(string? Database, List<ImageImportPlan> PlannedImports,
    List<ImageImportPending> Pending)
{
    public bool WritesAttempted => false;
    public bool MinioTouched => false;
    public bool DatabaseWritesAttempted => false;
}
