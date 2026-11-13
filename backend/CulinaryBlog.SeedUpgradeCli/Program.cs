using System.Text.Json;
using System.Text.Json.Serialization;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using CulinaryBlog.Infrastructure.Storage;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CulinaryBlog.SeedUpgradeCli;

var connection = Environment.GetEnvironmentVariable("SEED_UPGRADE_CONNECTION");
if (string.IsNullOrWhiteSpace(connection)) { Console.Error.WriteLine("SEED_UPGRADE_CONNECTION is required."); return 2; }
var command = args.FirstOrDefault();
var output = command == "seed-upgrade-snapshot"
    ? args.ElementAtOrDefault(1) ?? "seed-upgrade-snapshot"
    : Path.Combine(Directory.GetCurrentDirectory(), "seed-upgrade-report");
try
{
    if(command=="seed-upgrade-approval-template")
    {
        var snapshotPath=args.ElementAtOrDefault(1)??Path.Combine(Directory.GetCurrentDirectory(),"seed-upgrade-snapshot","full-snapshot.json");
        var snapshot=JsonSerializer.Deserialize<FullSeedSnapshot>(await File.ReadAllTextAsync(snapshotPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Snapshot is invalid.");
        var templateOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        await using var templateDb = new AppDbContext(templateOptions);
        var databaseName=templateDb.Database.GetDbConnection().Database;
        var approvals=snapshot.Recipes.Select(row=>{var n=int.Parse(row.Aggregate.Slug["lab02-recipe-".Length..]);return new SeedApproval(n,row.Aggregate.Id.ToString(),row.Aggregate.Slug,row.SnapshotHash,SeedUpgradeCatalog.Hash(n),false,false,databaseName);}).OrderBy(x=>x.RecipeNumber).ToList();
        if(ApprovalValidator.ValidateShape(approvals).Count!=0) throw new InvalidDataException("Approval template is invalid.");
        var path=args.ElementAtOrDefault(2)??"seed-upgrade-approvals.json"; await File.WriteAllTextAsync(path,JsonSerializer.Serialize(approvals,new JsonSerializerOptions{WriteIndented=true})); Console.WriteLine($"Approval template: {approvals.Count} {Path.GetFullPath(path)}"); return 0;
    }
    var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
    await using var db = new AppDbContext(options);
    if(command=="seed-image-import-dry-run")
    {
        var expectedDatabase=args.ElementAtOrDefault(1)??throw new ArgumentException("Expected database is required.");
        if(!string.Equals(db.Database.GetDbConnection().Database,expectedDatabase,StringComparison.Ordinal)) throw new InvalidOperationException("Connected database does not match expected database.");
        var reviewDirectory=args.ElementAtOrDefault(2)??"seed-upgrade-image-review";
        var approvalPath=args.ElementAtOrDefault(3);
        var result=await new ImageImportDryRun(db).AnalyzeAsync(reviewDirectory, approvalPath);
        var reportPath=args.ElementAtOrDefault(4)??Path.Combine(reviewDirectory,"image-import-cli-dry-run-report.json");
        await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Image import dry-run: planned={result.PlannedImports.Count} executable={result.PlannedImports.Count(item=>item.ExecutionAuthorized)} pending={result.Pending.Count} report={Path.GetFullPath(reportPath)}"); return 0;
    }
    if(command=="seed-image-import")
    {
        if(args.ElementAtOrDefault(1)!="--apply" || args.ElementAtOrDefault(2)!="--expected-database")
            throw new ArgumentException("Image import requires --apply --expected-database CulinaryBlogDb <review-dir> <approval-file> <report-file>.");
        var expectedDatabase=args.ElementAtOrDefault(3)??throw new ArgumentException("Expected database is required.");
        if(!string.Equals(db.Database.GetDbConnection().Database,expectedDatabase,StringComparison.Ordinal))
            throw new InvalidOperationException("Connected database does not match --expected-database.");
        var reviewDirectory=args.ElementAtOrDefault(4)??"seed-upgrade-image-review";
        var approvalPath=args.ElementAtOrDefault(5)??throw new ArgumentException("Approval file path is required.");
        var reportPath=args.ElementAtOrDefault(6)??Path.Combine(reviewDirectory,"image-import-report.json");
        var importPlan=await new ImageImportDryRun(db).AnalyzeAsync(reviewDirectory, approvalPath);
        if(importPlan.PlannedImports.Count!=100 || importPlan.PlannedImports.Any(item=>!item.ExecutionAuthorized))
            throw new InvalidOperationException("Import approvals do not authorize exactly 100 images.");
        var result=await new SeedImageImportService(db, CreateImageStorage()).ApplyAsync(importPlan.PlannedImports);
        await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Image import: imported={result.Imported} skippedExisting={result.SkippedExisting} failed={result.Failed} report={Path.GetFullPath(reportPath)}");
        return result.Failed==0?0:1;
    }
    if(command=="seed-upgrade-apply")
    {
        if(args.ElementAtOrDefault(1)!="--apply" || args.ElementAtOrDefault(2)!="--expected-database") throw new ArgumentException("Apply requires --apply --expected-database <database> <approval-file> <snapshot-file>.");
        var expectedDatabase=args.ElementAtOrDefault(3)??throw new ArgumentException("Expected database is required.");
        if(!string.Equals(db.Database.GetDbConnection().Database,expectedDatabase,StringComparison.Ordinal)) throw new InvalidOperationException("Connected database does not match --expected-database.");
        var approvalPath=args.ElementAtOrDefault(4)??throw new ArgumentException("Approval file path is required.");
        var snapshotPath=args.ElementAtOrDefault(5)??throw new ArgumentException("Snapshot file path is required.");
        var approvals=JsonSerializer.Deserialize<List<SeedApproval>>(await File.ReadAllTextAsync(approvalPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Approval file is invalid.");
        var snapshot=JsonSerializer.Deserialize<FullSeedSnapshot>(await File.ReadAllTextAsync(snapshotPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Snapshot is invalid.");
        var result=await new RealRecipeSeedUpgradeService(db).ApplyAsync(approvals,snapshot);
        Console.WriteLine($"Apply: upgraded={result.Upgraded} skipped={result.Skipped} rejected={result.Rejected} categoryRemapped={result.CategoryRemapped}"); return 0;
    }
    var before = await db.Recipes.IgnoreQueryFilters().CountAsync();
    if (command=="seed-upgrade-snapshot")
    {
        var seed=await db.Users.SingleAsync(x=>x.Email=="seed.author@culinaryblog.local");
        var aggregates=await db.Recipes.IgnoreQueryFilters().Include(x=>x.Nutrition).Include(x=>x.Ingredients).Include(x=>x.Steps).Include(x=>x.Images).Where(x=>x.AuthorId==seed.Id&&x.Slug.StartsWith("lab02-recipe-")).OrderBy(x=>x.Slug).ToListAsync();
        if(aggregates.Count!=100||aggregates.Select(x=>x.Slug).Distinct().Count()!=100) throw new InvalidOperationException("Expected exactly 100 unique seed recipes.");
        var aggregateRows=aggregates.OrderBy(x=>x.Slug).Select(SeedSnapshotFactory.CreateRow).ToList();
        var hash=SnapshotHashing.Hash(aggregateRows); var snapshot=new FullSeedSnapshot(hash,aggregateRows); Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output,"full-snapshot.json"),JsonSerializer.Serialize(snapshot,new JsonSerializerOptions{WriteIndented=true})); Console.WriteLine($"Snapshot: {aggregateRows.Count} {hash}"); return 0;
    }
    if(command=="seed-upgrade-validate-approvals")
    {
        var approvalPath=args.ElementAtOrDefault(1)??throw new ArgumentException("Approval file path is required.");
        var snapshotPath=args.ElementAtOrDefault(2)??throw new ArgumentException("Snapshot file path is required.");
        var approvals=JsonSerializer.Deserialize<List<SeedApproval>>(await File.ReadAllTextAsync(approvalPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Approval file is invalid.");
        var snapshot=JsonSerializer.Deserialize<FullSeedSnapshot>(await File.ReadAllTextAsync(snapshotPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Snapshot is invalid.");
        var validation=await ApprovalValidator.ValidateAsync(db,approvals,snapshot);
        var validationPath=args.ElementAtOrDefault(3)??"seed-upgrade-approval-validation.json";
        await File.WriteAllTextAsync(validationPath,JsonSerializer.Serialize(validation,new JsonSerializerOptions{WriteIndented=true,Converters={new JsonStringEnumConverter()}}));
        foreach(var group in validation.GroupBy(x=>x.Status).OrderBy(x=>x.Key)) Console.WriteLine($"{group.Key}: {group.Count()}");
        Console.WriteLine($"Approval validation: {Path.GetFullPath(validationPath)}"); return validation.Any(x=>x.Status is not ApprovalValidationStatus.NotApproved and not ApprovalValidationStatus.Valid and not ApprovalValidationStatus.AlreadyUpgraded)?1:0;
    }
    var rows = await new RealRecipeSeedUpgradeAnalyzer(db).AnalyzeAsync();
    var after = await db.Recipes.IgnoreQueryFilters().CountAsync();
    if (before != after) throw new InvalidOperationException("Dry-run changed database state.");
    Directory.CreateDirectory(output);
    await File.WriteAllTextAsync(Path.Combine(output,"report.json"), JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    var csv = "number,recipeId,slug,currentTitle,targetTitle,currentCategory,targetCategory,status,reason,ingredients,targetIngredients,steps,targetSteps,hasImages\n" + string.Join('\n', rows.Select(x => string.Join(',', new[]{x.Number.ToString(),x.RecipeId?.ToString()??"",x.Slug,x.CurrentTitle,x.TargetTitle,x.CurrentCategory,x.TargetCategory,x.Status,x.Reason,x.IngredientCount.ToString(),x.TargetIngredientCount.ToString(),x.StepCount.ToString(),x.TargetStepCount.ToString(),x.HasImages.ToString()}.Select(v=>"\""+(v??"").Replace("\"","\"\"")+"\""))));
    await File.WriteAllTextAsync(Path.Combine(output,"report.csv"),csv);
    var aggregateHashes = (await db.Recipes.IgnoreQueryFilters().Include(recipe=>recipe.Nutrition).Include(recipe=>recipe.Ingredients).Include(recipe=>recipe.Steps).Include(recipe=>recipe.Images).Where(recipe=>recipe.Slug.StartsWith("lab02-recipe-")).ToListAsync())
        .ToDictionary(recipe=>recipe.Slug,recipe=>SeedSnapshotFactory.CreateRow(recipe).SnapshotHash,StringComparer.Ordinal);
    var plan = rows.Select(x => new { recipeNumber=x.Number, recipeId=x.RecipeId, slug=x.Slug, currentTitle=x.CurrentTitle, targetTitle=x.TargetTitle, currentCategory=x.CurrentCategory, targetCategory=x.TargetCategory, currentIngredientCount=x.IngredientCount, targetIngredientCount=x.TargetIngredientCount, currentStepCount=x.StepCount, targetStepCount=x.TargetStepCount, analyzerStatus=x.Status, manualApproval=false, reason=x.Reason, aggregateSnapshotHash=aggregateHashes.GetValueOrDefault(x.Slug) }).ToList();
    await File.WriteAllTextAsync(Path.Combine(output,"migration-plan.json"), JsonSerializer.Serialize(plan,new JsonSerializerOptions{WriteIndented=true}));
    await File.WriteAllTextAsync(Path.Combine(output,"migration-plan.csv"), "number,recipeId,slug,status,manualApproval,aggregateSnapshotHash,reason\n"+string.Join('\n',plan.Select(x=>$"{x.recipeNumber},\"{x.recipeId}\",\"{x.slug}\",{x.analyzerStatus},false,{x.aggregateSnapshotHash},\"{x.reason.Replace("\"","\"\"")}\"")));
    foreach(var group in rows.GroupBy(x=>x.Status).OrderBy(x=>x.Key)) Console.WriteLine($"{group.Key}: {group.Count()}");
    Console.WriteLine($"Report: {Path.GetFullPath(output)}"); return 0;
}
catch(Exception ex){ Console.Error.WriteLine($"Dry-run failed: {ex.Message}"); return 1; }

static IFileStorageService CreateImageStorage()
{
    var endpoint=Required("SEED_IMAGE_MINIO_ENDPOINT");
    var accessKey=Required("SEED_IMAGE_MINIO_ACCESS_KEY");
    var secretKey=Required("SEED_IMAGE_MINIO_SECRET_KEY");
    var publicBaseUrl=Environment.GetEnvironmentVariable("SEED_IMAGE_MINIO_PUBLIC_BASE_URL");
    var useSsl=bool.TryParse(Environment.GetEnvironmentVariable("SEED_IMAGE_MINIO_USE_SSL"),out var value)&&value;
    var options=Options.Create(new MinioOptions { Endpoint=endpoint, AccessKey=accessKey, SecretKey=secretKey,
        Bucket="culinary-blog", PublicBaseUrl=publicBaseUrl, UseSsl=useSsl });
    return new MinioFileStorageService(options,new ImageFileValidator(),new RecipeImageResizer(),
        new NoopImageCleanupQueue(),NullLogger<MinioFileStorageService>.Instance);
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException($"{name} is required.");

sealed class NoopImageCleanupQueue : IImageCleanupQueue
{
    public void EnqueueDelete(string objectKey) { }
}
