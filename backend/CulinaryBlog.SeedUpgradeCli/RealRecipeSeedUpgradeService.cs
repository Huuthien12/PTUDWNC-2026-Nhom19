using System.Data;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.SeedUpgradeCli;

public sealed record SeedUpgradeApplyResult(int Upgraded, int Skipped, int Rejected, int CategoryRemapped);

public sealed class RealRecipeSeedUpgradeService(AppDbContext db)
{
    public async Task<SeedUpgradeApplyResult> ApplyAsync(IReadOnlyList<SeedApproval> approvals,
        FullSeedSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var validation = await ApprovalValidator.ValidateAsync(db, approvals, snapshot, cancellationToken);
        var eligible = validation.Where(result => result.Status == ApprovalValidationStatus.Valid)
            .Select(result => result.RecipeNumber).ToHashSet();
        var rejected = validation.Count(result => result.Status is not ApprovalValidationStatus.Valid and not ApprovalValidationStatus.NotApproved and not ApprovalValidationStatus.AlreadyUpgraded);
        var skipped = validation.Count(result => result.Status is ApprovalValidationStatus.NotApproved or ApprovalValidationStatus.AlreadyUpgraded);
        if (eligible.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(0, skipped, rejected, 0);
        }

        var seedRecipes = await db.Recipes.IgnoreQueryFilters().Include(recipe => recipe.Nutrition).Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Steps).Include(recipe => recipe.Images)
            .Where(recipe => recipe.Slug.StartsWith("lab02-recipe-")).ToListAsync(cancellationToken);
        var recipes = seedRecipes.Where(recipe => eligible.Contains(ParseRecipeNumber(recipe.Slug))).ToList();
        var categories = await db.Categories.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var remapped = 0;
        foreach (var recipe in recipes)
        {
            var number = ParseRecipeNumber(recipe.Slug);
            var approval = approvals.Single(item => item.RecipeNumber == number);
            var target = SeedUpgradeCatalog.Snapshot(number);
            if (SeedSnapshotFactory.CreateRow(recipe).SnapshotHash != approval.SnapshotHash)
                throw new InvalidOperationException($"Recipe {number} changed before apply.");
            var contentMatches = ApprovalValidator.MatchesCatalogContent(recipe, number);

            var category = ApprovalValidator.ResolveTargetCategory(categories, approval)
                ?? throw new InvalidOperationException($"Recipe {number} target category is missing or ambiguous.");
            if (recipe.CategoryId != category.Id)
            {
                if (!approval.ApproveCategoryRemap) throw new InvalidOperationException($"Recipe {number} category remap is not approved.");
                recipe.CategoryId = category.Id;
                remapped++;
            }

            if (!contentMatches)
            {
                db.RecipeIngredients.RemoveRange(recipe.Ingredients);
                db.RecipeSteps.RemoveRange(recipe.Steps);
                recipe.Title = target.Title;
                recipe.Description = target.Description;
                recipe.Instructions = target.Instructions;
                recipe.PrepTime = target.PrepTime;
                recipe.CookTime = target.CookTime;
                recipe.Servings = target.Servings;
                recipe.Difficulty = (CulinaryBlog.Domain.Enums.RecipeDifficulty)target.Difficulty;
                if (recipe.Nutrition is not null)
                {
                    recipe.Nutrition.Calories = null;
                    recipe.Nutrition.Protein = null;
                    recipe.Nutrition.Carbs = null;
                    recipe.Nutrition.Fat = null;
                }
                db.RecipeIngredients.AddRange(target.Ingredients.Select(ingredient => new RecipeIngredient
                {
                    RecipeId = recipe.Id, Name = ingredient.Name, Quantity = ingredient.Quantity, Unit = ingredient.Unit,
                    Notes = ingredient.Notes, OrderIndex = ingredient.OrderIndex
                }));
                db.RecipeSteps.AddRange(target.Steps.Select(step => new RecipeStep
                {
                    RecipeId = recipe.Id, StepNumber = step.StepNumber, Title = step.Title,
                    Description = step.Description, TimerMinutes = step.TimerMinutes
                }));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(recipes.Count, skipped, rejected, remapped);
    }

    private static int ParseRecipeNumber(string slug) => int.Parse(slug["lab02-recipe-".Length..]);
}
