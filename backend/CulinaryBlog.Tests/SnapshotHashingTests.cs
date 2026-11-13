using CulinaryBlog.SeedUpgradeCli;
using CulinaryBlog.Domain.Entities;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class SnapshotHashingTests
{
    private sealed record Ingredient(Guid Id, decimal Quantity, string? Unit);
    private sealed record Step(Guid Id, string Description);
    private sealed record Image(Guid Id, string Url);
    private sealed record Nutrition(decimal? Calories, decimal? Protein, decimal? Carbs, decimal? Fat);
    private sealed record Aggregate(Guid Id, string Description, Nutrition Nutrition, Ingredient[] Ingredients, Step[] Steps, Image[] Images);
    private static Aggregate Sample() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "one", new(100, 5, 10, 2), [new(Guid.Parse("22222222-2222-2222-2222-222222222222"), 10, "g")], [new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "step")], [new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "recipes/x")]);
    [Fact] public void Same_aggregate_has_same_64_hex_hash() { var x=Sample(); Assert.Equal(SnapshotHashing.Hash(x), SnapshotHashing.Hash(x)); Assert.Matches("^[0-9a-f]{64}$",SnapshotHashing.Hash(x)); }
    [Fact] public void Meaningful_changes_change_hash() { var x=Sample(); Assert.NotEqual(SnapshotHashing.Hash(x),SnapshotHashing.Hash(x with { Ingredients=[x.Ingredients[0] with { Quantity=11 }] })); Assert.NotEqual(SnapshotHashing.Hash(x),SnapshotHashing.Hash(x with { Steps=[x.Steps[0] with { Description="changed" }] })); Assert.NotEqual(SnapshotHashing.Hash(x),SnapshotHashing.Hash(x with { Nutrition=x.Nutrition with { Calories=101 } })); Assert.NotEqual(SnapshotHashing.Hash(x),SnapshotHashing.Hash(x with { Images=[x.Images[0] with { Url="recipes/y" }] })); }
    [Fact] public void Json_round_trip_preserves_hash() { var x=Sample(); var copy=System.Text.Json.JsonSerializer.Deserialize<Aggregate>(SnapshotHashing.Json(x))!; Assert.Equal(SnapshotHashing.Hash(x),SnapshotHashing.Hash(copy)); }

    [Fact]
    public void Target_catalog_hash_covers_recipe_content_not_just_title()
    {
        var target=SeedUpgradeCatalog.Snapshot(1);
        Assert.NotEqual(SnapshotHashing.Hash(target),SnapshotHashing.Hash(target with { Description="changed" }));
        Assert.NotEqual(SnapshotHashing.Hash(target),SnapshotHashing.Hash(target with { Ingredients=[..target.Ingredients, new(99,"extra",1,"g",null)] }));
        Assert.NotEqual(SnapshotHashing.Hash(target),SnapshotHashing.Hash(target with { Steps=[..target.Steps, new(99,"extra","changed",1)] }));
    }

    [Fact]
    public void Approval_shape_requires_all_unique_recipe_records()
    {
        var approvals=Enumerable.Range(1,100).Select(number=>new SeedApproval(number,Guid.NewGuid().ToString(),$"lab02-recipe-{number}",new string('a',64),new string('b',64),false,false,"test")).ToList();
        Assert.Empty(ApprovalValidator.ValidateShape(approvals));
        approvals[1]=approvals[1] with { RecipeNumber=1 };
        Assert.Contains(ApprovalValidator.ValidateShape(approvals),result=>result.Status==ApprovalValidationStatus.Duplicate);
    }

    [Fact]
    public void Approval_shape_rejects_missing_or_malformed_hashes()
    {
        var approvals=Enumerable.Range(1,100).Select(number=>new SeedApproval(number,Guid.NewGuid().ToString(),$"lab02-recipe-{number}",new string('a',64),new string('b',64),false,false,"test")).ToList();
        Assert.Contains(ApprovalValidator.ValidateShape(approvals.Take(99).ToList()),result=>result.Status==ApprovalValidationStatus.Missing&&result.RecipeNumber==100);
        approvals[0]=approvals[0] with { SnapshotHash="not-a-hash" };
        Assert.Contains(ApprovalValidator.ValidateShape(approvals),result=>result.Status==ApprovalValidationStatus.Conflict&&result.RecipeNumber==1);
    }

    [Fact]
    public void Explicit_target_category_id_resolves_the_administrator_selected_category()
    {
        var accented = new Category { Id = Guid.NewGuid(), Name = "Món Việt", Slug = "mon-viet-accented" };
        var unaccented = new Category { Id = Guid.NewGuid(), Name = "Mon Viet", Slug = "mon-viet" };
        var approval = new SeedApproval(1, Guid.NewGuid().ToString(), "lab02-recipe-1", new string('a', 64),
            new string('b', 64), false, false, "fixture", accented.Id.ToString());

        Assert.Same(accented, ApprovalValidator.ResolveTargetCategory([accented, unaccented], approval));
        Assert.Same(unaccented, ApprovalValidator.ResolveTargetCategory([accented, unaccented], approval with { TargetCategoryId = unaccented.Id.ToString() }));
        Assert.Null(ApprovalValidator.ResolveTargetCategory([accented, unaccented], approval with { TargetCategoryId = Guid.NewGuid().ToString() }));
    }

    [Fact]
    public void Image_import_dry_run_uses_a_stable_storage_compatible_upload_id()
    {
        const string recipeId = "11111111-1111-1111-1111-111111111111";
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var first = ImageImportDryRun.StableUploadId(recipeId, hash);
        Assert.Equal(first, ImageImportDryRun.StableUploadId(recipeId, hash));
        Assert.NotEqual(first, ImageImportDryRun.StableUploadId(recipeId, hash.Replace('a', 'b')));
        Assert.Matches("^[0-9a-f]{32}$", first.ToString("N"));
    }
}
