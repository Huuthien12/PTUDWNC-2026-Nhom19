using CulinaryBlog.Infrastructure.Persistence.Seed;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RealRecipeSeedBatch02Tests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedCategories =
        new Dictionary<string, string>
        {
            ["Bún riêu cua"] = "Món Việt",
            ["Cà ri gà Nhật Bản"] = "Món Á",
            ["Gà sốt kem nấm"] = "Món Âu",
            ["Nấm đùi gà kho tiêu"] = "Món Chay",
            ["Gà nướng mật ong"] = "Món Nướng",
            ["Cá diêu hồng chiên giòn"] = "Món Chiên",
            ["Mực xào cần tỏi"] = "Món Xào",
            ["Trứng hấp thịt băm"] = "Món Hấp",
            ["Cá basa kho tiêu"] = "Món Kho",
            ["Canh rau ngót thịt băm"] = "Món Canh",
            ["Súp gà ngô non"] = "Món Súp",
            ["Salad cá ngừ"] = "Món Salad",
            ["Xôi mặn thập cẩm"] = "Món Ăn Sáng",
            ["Bún thịt nướng"] = "Món Ăn Trưa",
            ["Lẩu gà nấm"] = "Món Ăn Tối",
            ["Bánh tráng trộn"] = "Món Ăn Vặt",
            ["Bánh chuối nướng"] = "Bánh Ngọt",
            ["Chè khúc bạch"] = "Tráng Miệng",
            ["Sinh tố bơ"] = "Đồ Uống",
            ["Mực nướng sa tế"] = "Hải Sản"
        };

    [Fact]
    public void Batch02_has_twenty_complete_distinct_recipes()
    {
        var recipes = RealRecipeSeedBatch02.Items;

        Assert.Equal(20, recipes.Count);
        Assert.Equal(ExpectedCategories.Keys.Order(), recipes.Select(recipe => recipe.Title).Order());
        Assert.Equal(20, recipes.Select(recipe => recipe.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var recipe in recipes)
        {
            Assert.Equal(ExpectedCategories[recipe.Title], recipe.CategoryName);
            Assert.False(string.IsNullOrWhiteSpace(recipe.Description));
            Assert.InRange(recipe.PrepTime, 1, 240);
            Assert.InRange(recipe.CookTime, 1, 360);
            Assert.InRange(recipe.Servings, 1, 12);
            Assert.InRange(recipe.Ingredients.Count, 5, 12);
            Assert.InRange(recipe.Steps.Count, 4, 8);
            Assert.All(recipe.Ingredients, ingredient =>
            {
                Assert.False(string.IsNullOrWhiteSpace(ingredient.Name));
                Assert.True(ingredient.Quantity > 0);
                Assert.Contains(ingredient.Unit, new[] { "bát", "bìa", "cây", "củ", "g", "kg", "lít", "ml", "quả", "tép", "thìa canh", "thìa cà phê" });
            });
            Assert.All(recipe.Steps, step =>
            {
                Assert.False(string.IsNullOrWhiteSpace(step.Title));
                Assert.False(string.IsNullOrWhiteSpace(step.Description));
                Assert.True(step.TimerMinutes is > 0);
            });
        }
    }
}
