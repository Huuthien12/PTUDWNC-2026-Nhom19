using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public sealed record SeedUpgradeAnalysis(int Number, Guid? RecipeId, string Slug, string? CurrentTitle, string TargetTitle, string? CurrentCategory, string TargetCategory, string Status, string Reason, int IngredientCount, int TargetIngredientCount, int StepCount, int TargetStepCount, bool HasImages, bool AuthorValidated = false, bool LegacyContentValidated = false, string IngredientValidationReason = "", string StepValidationReason = "");

public sealed class RealRecipeSeedUpgradeAnalyzer(AppDbContext db)
{
    public async Task<IReadOnlyList<SeedUpgradeAnalysis>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var catalog = RealRecipeSeedBatch01.Items.Concat(RealRecipeSeedBatch02.Items).Concat(RealRecipeSeedBatch03.Items).Concat(RealRecipeSeedBatch04.Items).Concat(RealRecipeSeedBatch05.Items).ToList();
        var recipes = await db.Recipes.IgnoreQueryFilters().Include(x => x.Author).Include(x => x.Category).Include(x => x.Nutrition).Include(x => x.Ingredients).Include(x => x.Steps).Include(x => x.Images).Where(x => x.Slug.StartsWith("lab02-recipe-")).ToListAsync(cancellationToken);
        return catalog.Select((target, i) => Analyze(i + 1, target, recipes.SingleOrDefault(x => x.Slug == $"lab02-recipe-{i + 1}"))).ToList();
    }
    private static SeedUpgradeAnalysis Analyze(int n, RealRecipeSeed t, Recipe? r)
    {
        var slug=$"lab02-recipe-{n}"; if(r is null)return new(n,null,slug,null,t.Title,null,t.CategoryName,"MISSING","Seed slug was not found.",0,t.Ingredients.Count,0,t.Steps.Count,false);
        var author=r.Author.Email=="seed.author@culinaryblog.local";
        var legacy=r.Title==$"Công thức #{n:00}"&&r.Description.StartsWith("Một công thức tham khảo")&&r.Instructions=="Thực hiện theo các bước chế biến bên dưới."&&r.Ingredients.Count(x=>!x.IsDeleted)==10&&r.Steps.Count(x=>!x.IsDeleted)==5;
        var contentMatches=MatchesCatalogContent(r,t);
        var categoryMatches=r.Category.Name==t.CategoryName;
        var status=contentMatches?(categoryMatches?"ALREADY_UPGRADED":"CONTENT_UPGRADED_CATEGORY_REMAP_REQUIRED"):!author?"CONFLICT":legacy?"CONFLICT":"MODIFIED";
        var reason=contentMatches?(categoryMatches?"Matches full catalog aggregate.":"Content matches the full catalog aggregate, but the current category requires an explicit remap."):!author?"Seed author email did not match.":legacy?"Legacy random ingredient/step values have no immutable baseline; cannot prove untouched.":"Legacy title, description, instructions or child values changed.";
        return new(n,r.Id,slug,r.Title,t.Title,r.Category.Name,t.CategoryName,status,reason,r.Ingredients.Count(x=>!x.IsDeleted),t.Ingredients.Count,r.Steps.Count(x=>!x.IsDeleted),t.Steps.Count,r.Images.Any(x=>!x.IsDeleted),author,false,"No immutable Bogus ingredient baseline exists.","No immutable Bogus step baseline exists.");
    }

    private static bool MatchesCatalogContent(Recipe recipe, RealRecipeSeed target) =>
        recipe.Title==target.Title && recipe.Description==target.Description &&
        recipe.Instructions==string.Join("\n\n",target.Steps.Select(step=>$"{step.Title}: {step.Description}")) &&
        recipe.PrepTime==target.PrepTime && recipe.CookTime==target.CookTime && recipe.Servings==target.Servings &&
        recipe.Difficulty==target.Difficulty && (recipe.Nutrition is null || (recipe.Nutrition.Calories is null && recipe.Nutrition.Protein is null && recipe.Nutrition.Carbs is null && recipe.Nutrition.Fat is null)) &&
        recipe.Ingredients.Where(item=>!item.IsDeleted).OrderBy(item=>item.OrderIndex).Select(item=>new { item.Name,item.Quantity,item.Unit,item.Notes,item.OrderIndex })
            .SequenceEqual(target.Ingredients.Select((item,index)=>new { item.Name,Quantity=(decimal?)item.Quantity,Unit=(string?)item.Unit,item.Notes,OrderIndex=index+1 })) &&
        recipe.Steps.Where(item=>!item.IsDeleted).OrderBy(item=>item.StepNumber).Select(item=>new { item.StepNumber,item.Title,item.Description,item.TimerMinutes })
            .SequenceEqual(target.Steps.Select((item,index)=>new { StepNumber=index+1,Title=(string?)item.Title,item.Description,item.TimerMinutes }));
    private static string Normalize(string s)=>string.Concat(s.Normalize(System.Text.NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark)).Replace(" ","").ToLowerInvariant();
}
