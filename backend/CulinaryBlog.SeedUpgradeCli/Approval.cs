using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.SeedUpgradeCli;

public sealed record SeedApproval(int RecipeNumber, string RecipeId, string Slug, string SnapshotHash,
    string TargetCatalogHash, bool ApproveReplacement, bool ApproveCategoryRemap, string DatabaseName,
    string? TargetCategoryId = null);

public enum ApprovalValidationStatus
{
    Valid, AlreadyUpgraded, NotApproved, InvalidSnapshotHash, InvalidCatalogHash, InvalidOwner,
    InvalidCategoryApproval, Duplicate, Missing, Conflict
}

public sealed record ApprovalValidationResult(int RecipeNumber, ApprovalValidationStatus Status, string Reason);

public static class ApprovalValidator
{
    public static IReadOnlyList<ApprovalValidationResult> ValidateShape(IReadOnlyList<SeedApproval> approvals)
    {
        var results = new List<ApprovalValidationResult>();
        for (var number = 1; number <= 100; number++)
        {
            var matches = approvals.Where(approval => approval.RecipeNumber == number).ToList();
            if (matches.Count == 0) { results.Add(new(number, ApprovalValidationStatus.Missing, "Approval record is missing.")); continue; }
            if (matches.Count > 1 || approvals.Count(approval => approval.RecipeId == matches[0].RecipeId) > 1 || approvals.Count(approval => approval.Slug == matches[0].Slug) > 1)
            { results.Add(new(number, ApprovalValidationStatus.Duplicate, "Recipe number, ID, or slug is duplicated.")); continue; }
            var approval = matches[0];
            if (!Guid.TryParse(approval.RecipeId, out _) || approval.Slug != $"lab02-recipe-{number}" || !IsHash(approval.SnapshotHash) || !IsHash(approval.TargetCatalogHash) || string.IsNullOrWhiteSpace(approval.DatabaseName) || (!string.IsNullOrWhiteSpace(approval.TargetCategoryId) && !Guid.TryParse(approval.TargetCategoryId, out _)))
                results.Add(new(number, ApprovalValidationStatus.Conflict, "Approval fields are malformed."));
        }
        return results;
    }

    public static async Task<IReadOnlyList<ApprovalValidationResult>> ValidateAsync(AppDbContext db,
        IReadOnlyList<SeedApproval> approvals, FullSeedSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var shape = ValidateShape(approvals).ToDictionary(result => result.RecipeNumber);
        var snapshotRows = snapshot.Recipes.ToDictionary(row => row.Aggregate.Slug, StringComparer.Ordinal);
        var seedAuthor = await db.Users.SingleOrDefaultAsync(user => user.Email == "seed.author@culinaryblog.local", cancellationToken);
        var categories = await db.Categories.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var ids = approvals.Where(approval => Guid.TryParse(approval.RecipeId, out _)).Select(approval => Guid.Parse(approval.RecipeId)).ToList();
        var recipes = await db.Recipes.IgnoreQueryFilters().Include(recipe => recipe.Nutrition).Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Steps).Include(recipe => recipe.Images).Where(recipe => ids.Contains(recipe.Id)).ToListAsync(cancellationToken);
        var databaseName = db.Database.GetDbConnection().Database;
        var results = new List<ApprovalValidationResult>();

        for (var number = 1; number <= 100; number++)
        {
            if (shape.TryGetValue(number, out var shapeFailure)) { results.Add(shapeFailure); continue; }
            var approval = approvals.Single(item => item.RecipeNumber == number);
            if (!string.Equals(approval.DatabaseName, databaseName, StringComparison.Ordinal))
            { results.Add(new(number, ApprovalValidationStatus.Conflict, "Approval was created for a different database.")); continue; }
            if (!snapshotRows.TryGetValue(approval.Slug, out var snapshotRow) || approval.SnapshotHash != snapshotRow.SnapshotHash)
            { results.Add(new(number, ApprovalValidationStatus.InvalidSnapshotHash, "Approval snapshot hash does not match the supplied snapshot.")); continue; }
            if (approval.TargetCatalogHash != SeedUpgradeCatalog.Hash(number))
            { results.Add(new(number, ApprovalValidationStatus.InvalidCatalogHash, "Target catalog changed after approval creation.")); continue; }

            var recipe = recipes.SingleOrDefault(item => item.Id == Guid.Parse(approval.RecipeId));
            if (seedAuthor is null || recipe is null || recipe.AuthorId != seedAuthor.Id || recipe.Slug != approval.Slug)
            { results.Add(new(number, ApprovalValidationStatus.InvalidOwner, "Recipe does not match the seed author, ID, and slug.")); continue; }
            var targetCategory = ResolveTargetCategory(categories, approval);
            if (targetCategory is null)
            { results.Add(new(number, ApprovalValidationStatus.Conflict, "Target category is missing or not unique.")); continue; }
            var contentMatches = MatchesCatalogContent(recipe, number);
            if (contentMatches && recipe.CategoryId == targetCategory.Id)
            { results.Add(new(number, ApprovalValidationStatus.AlreadyUpgraded, "Recipe already matches the current catalog.")); continue; }
            if (SeedSnapshotFactory.CreateRow(recipe).SnapshotHash != approval.SnapshotHash)
            { results.Add(new(number, ApprovalValidationStatus.InvalidSnapshotHash, "Recipe aggregate changed after the snapshot was created.")); continue; }
            if (recipe.CategoryId != targetCategory.Id && !approval.ApproveCategoryRemap)
            { results.Add(new(number, ApprovalValidationStatus.InvalidCategoryApproval, "Category remap has not been approved.")); continue; }
            if (contentMatches)
            { results.Add(new(number, ApprovalValidationStatus.Valid, "Category remap is approved; content already matches the catalog.")); continue; }
            results.Add(!approval.ApproveReplacement
                ? new(number, ApprovalValidationStatus.NotApproved, "Replacement has not been approved.")
                : new(number, ApprovalValidationStatus.Valid, "Approval matches the current snapshot and catalog."));
        }
        return results;
    }

    private static bool IsHash(string? value) => value?.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static CulinaryBlog.Domain.Entities.Category? ResolveTargetCategory(
        IReadOnlyCollection<CulinaryBlog.Domain.Entities.Category> categories, SeedApproval approval)
    {
        var targetName = SeedUpgradeCatalog.Snapshot(approval.RecipeNumber).CategoryName;
        if (!string.IsNullOrWhiteSpace(approval.TargetCategoryId))
        {
            return Guid.TryParse(approval.TargetCategoryId, out var targetId)
                ? categories.SingleOrDefault(category => category.Id == targetId)
                : null;
        }

        var matches = categories.Where(category => SeedUpgradeCatalog.NormalizeCategory(category.Name) ==
            SeedUpgradeCatalog.NormalizeCategory(targetName)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public static bool MatchesCatalogContent(CulinaryBlog.Domain.Entities.Recipe recipe, int number)
    {
        var target = SeedUpgradeCatalog.Snapshot(number);
        return recipe.Title == target.Title && recipe.Description == target.Description && recipe.Instructions == target.Instructions &&
            recipe.PrepTime == target.PrepTime && recipe.CookTime == target.CookTime &&
            recipe.Servings == target.Servings && (int)recipe.Difficulty == (int)target.Difficulty &&
            (recipe.Nutrition is null || (recipe.Nutrition.Calories is null && recipe.Nutrition.Protein is null && recipe.Nutrition.Carbs is null && recipe.Nutrition.Fat is null)) &&
            recipe.Ingredients.Where(item => !item.IsDeleted).OrderBy(item => item.OrderIndex).Select(item => new { item.Name, item.Quantity, item.Unit, item.Notes, item.OrderIndex })
                .SequenceEqual(target.Ingredients.Select(item => new { item.Name, Quantity = (decimal?)item.Quantity, Unit = (string?)item.Unit, item.Notes, item.OrderIndex })) &&
            recipe.Steps.Where(item => !item.IsDeleted).OrderBy(item => item.StepNumber).Select(item => new { item.StepNumber, item.Title, item.Description, item.TimerMinutes })
                .SequenceEqual(target.Steps.Select(item => new { item.StepNumber, Title = (string?)item.Title, item.Description, item.TimerMinutes }));
    }
}
