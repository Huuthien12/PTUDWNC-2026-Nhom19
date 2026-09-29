using Bogus;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public static class DataSeeder
{
    private const int RequiredCategories = 20;
    private const int RequiredRecipes = 100;
    private const int IngredientsPerRecipe = 10;
    private const int StepsPerRecipe = 5;

    private static readonly IReadOnlyDictionary<string, string> CategoryDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Món Việt"] = "Hương vị Việt thân quen, từ món ngon ba miền đến bữa cơm nhà ấm cúng.",
            ["Món Á"] = "Khám phá những sắc màu ẩm thực châu Á qua các cách kết hợp nguyên liệu và gia vị.",
            ["Món Âu"] = "Gợi ý món ăn phong cách châu Âu để làm mới thực đơn trong căn bếp của bạn.",
            ["Món Chay"] = "Cảm hứng từ rau củ, nấm và đậu cho những bữa ăn chay đa dạng.",
            ["Món Nướng"] = "Những ý tưởng món nướng thơm lừng dành cho bữa ăn sum họp.",
            ["Món Chiên"] = "Khám phá các món chiên vàng giòn, thêm chút phong phú cho bữa cơm.",
            ["Món Xào"] = "Các món xào nhanh gọn, kết hợp nguyên liệu tươi ngon cho bữa ăn hằng ngày.",
            ["Món Hấp"] = "Các món hấp thanh nhẹ, giữ được hương vị tự nhiên của nguyên liệu.",
            ["Món Kho"] = "Những món kho đậm đà, gợi nhớ hương vị bữa cơm gia đình.",
            ["Món Canh"] = "Những món canh quen thuộc, phù hợp cho bữa cơm gia đình.",
            ["Món Súp"] = "Một bát súp ấm áp để bắt đầu bữa ăn hoặc tận hưởng một ngày thong thả.",
            ["Món Salad"] = "Kết hợp rau củ và nước xốt để mang đến những món salad tươi mới.",
            ["Món Ăn Sáng"] = "Gợi ý cho bữa sáng, từ đơn giản nhanh gọn đến những ngày có thời gian vào bếp.",
            ["Món Ăn Trưa"] = "Ý tưởng đổi món cho bữa trưa ở nhà hoặc chuẩn bị mang theo.",
            ["Món Ăn Tối"] = "Cùng quây quần bên những món ăn dành cho bữa tối sau một ngày dài.",
            ["Món Ăn Vặt"] = "Những gợi ý nhâm nhi cho buổi chiều thư thả và những cuộc trò chuyện vui.",
            ["Bánh Ngọt"] = "Cảm hứng làm bánh cho những ai yêu mùi thơm dịu từ căn bếp.",
            ["Tráng Miệng"] = "Một chút ngọt ngào để khép lại bữa ăn và chia sẻ cùng người thân.",
            ["Đồ Uống"] = "Những ý tưởng pha chế để bạn tìm thấy thức uống yêu thích của mình.",
            ["Hải Sản"] = "Khám phá cách chế biến hải sản để làm phong phú thực đơn gia đình."
        };

    private static string RecipeDescription(string categoryName) =>
        $"Một công thức tham khảo trong danh mục {categoryName}. " +
        "Bạn có thể điều chỉnh nguyên liệu và cách chế biến theo sở thích.";

    private static readonly string[] StepDescriptions =
    [
        "Đọc danh sách nguyên liệu và chuẩn bị dụng cụ cần thiết trước khi bắt đầu.",
        "Sơ chế và chia nguyên liệu thành các phần phù hợp với cách chế biến đã chọn.",
        "Chuẩn bị phần gia vị, điều chỉnh lượng dùng theo khẩu vị và số người ăn.",
        "Chế biến theo phương pháp phù hợp với từng nguyên liệu; kiểm tra độ chín trước khi dùng.",
        "Hoàn thiện cách trình bày và thưởng thức. Ghi lại những điều chỉnh cho lần vào bếp tiếp theo."
    ];

    // Upgrade only recognizable, untouched legacy demo content. Keep IDs, slugs,
    // relationships and any recipes edited after creation intact.
    private static async Task RefreshLegacyRecipeContentAsync(
        AppDbContext context, ApplicationUser author, IReadOnlyList<Category> categories)
    {
        var legacyRecipes = await context.Recipes
            .Where(recipe => recipe.AuthorId == author.Id &&
                recipe.Slug.StartsWith("lab02-recipe-") &&
                recipe.Title.StartsWith("Công thức Lab 02 số "))
            .Include(recipe => recipe.Steps)
            .ToListAsync();
        var categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);

        foreach (var recipe in legacyRecipes)
        {
            var suffix = recipe.Slug["lab02-recipe-".Length..];
            if (!int.TryParse(suffix, out var number) || number < 1 || number > RequiredRecipes ||
                recipe.Title != $"Công thức Lab 02 số {number}" ||
                recipe.UpdatedAt > recipe.CreatedAt.AddSeconds(1) ||
                !categoryNames.TryGetValue(recipe.CategoryId, out var categoryName))
                continue;

            recipe.Title = $"Công thức #{number:00}";
            recipe.Description = RecipeDescription(categoryName);
            foreach (var step in recipe.Steps.Where(step =>
                step.StepNumber >= 1 && step.StepNumber <= StepsPerRecipe &&
                step.Title == $"Bước {step.StepNumber}" &&
                step.UpdatedAt <= step.CreatedAt.AddSeconds(1)))
                step.Description = StepDescriptions[step.StepNumber - 1];
        }
        await context.SaveChangesAsync();
    }

    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        // Cố định seed để dữ liệu có thể tái tạo.
        Randomizer.Seed = new Random(2312753);

        Console.WriteLine("===== LAB 02 DATA SEED =====");

        var author = await SeedAuthorAsync(userManager);

        var categories = await SeedCategoriesAsync(context);

        await RefreshLegacyRecipeContentAsync(context, author, categories);

        await SeedRecipesAsync(
            context,
            author,
            categories);

        Console.WriteLine("===== SEED COMPLETED =====");
    }

    // =========================================================
    // AUTHOR
    // =========================================================

    private static async Task<ApplicationUser> SeedAuthorAsync(
        UserManager<ApplicationUser> userManager)
    {
        const string email =
            "seed.author@culinaryblog.local";

        var existingUser =
            await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            Console.WriteLine("Seed author already exists.");

            return existingUser;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Lab 02 Seed Author",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(
            user,
            "Seed@123456");

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(
                    error => error.Description));

            throw new InvalidOperationException(
                $"Cannot create seed author: {errors}");
        }

        Console.WriteLine("Created seed author.");

        return user;
    }

    // =========================================================
    // CATEGORIES
    // =========================================================

    private static async Task<List<Category>> SeedCategoriesAsync(
        AppDbContext context)
    {
        var categories =
            await context.Categories
                .OrderBy(category => category.CreatedAt)
                .ToListAsync();

        var categoryNames = new[]
        {
            "Món Việt",
            "Món Á",
            "Món Âu",
            "Món Chay",
            "Món Nướng",
            "Món Chiên",
            "Món Xào",
            "Món Hấp",
            "Món Kho",
            "Món Canh",
            "Món Súp",
            "Món Salad",
            "Món Ăn Sáng",
            "Món Ăn Trưa",
            "Món Ăn Tối",
            "Món Ăn Vặt",
            "Bánh Ngọt",
            "Tráng Miệng",
            "Đồ Uống",
            "Hải Sản"
        };

        foreach (var category in categories)
        {
            if (category.Slug.StartsWith("lab02-category-", StringComparison.Ordinal) &&
                category.Description == $"Danh mục dữ liệu mẫu Lab 02 - {category.Name}" &&
                CategoryDescriptions.TryGetValue(category.Name, out var description))
                category.Description = description;
        }

        var existingNames =
            categories
                .Select(category => category.Name)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var existingSlugs =
            categories
                .Select(category => category.Slug)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        for (var i = 0;
             i < categoryNames.Length &&
             categories.Count < RequiredCategories;
             i++)
        {
            var name = categoryNames[i];

            if (existingNames.Contains(name))
            {
                continue;
            }

            var baseSlug =
                $"lab02-category-{i + 1}";

            var slug = baseSlug;
            var suffix = 2;

            while (existingSlugs.Contains(slug))
            {
                slug =
                    $"{baseSlug}-{suffix}";

                suffix++;
            }

            var category = Category.Create(
                name,
                slug,
                CategoryDescriptions[name]);

            context.Categories.Add(category);

            categories.Add(category);

            existingNames.Add(name);
            existingSlugs.Add(slug);
        }

        await context.SaveChangesAsync();

        if (categories.Count < RequiredCategories)
        {
            throw new InvalidOperationException(
                "Database must contain at least 20 categories.");
        }

        Console.WriteLine(
            $"Categories: {categories.Count}");

        return categories;
    }

    // =========================================================
    // RECIPES
    // =========================================================

    private static async Task SeedRecipesAsync(
        AppDbContext context,
        ApplicationUser author,
        IReadOnlyList<Category> categories)
    {
        var existingRecipes =
            await context.Recipes
                .IgnoreQueryFilters()
                .CountAsync();

        if (existingRecipes >= RequiredRecipes)
        {
            Console.WriteLine(
                $"Recipes already exist: {existingRecipes}");

            return;
        }

        var faker = new Faker("vi");

        var ingredientNames = new[]
        {
            "Thịt heo",
            "Thịt bò",
            "Thịt gà",
            "Cá",
            "Tôm",
            "Trứng",
            "Cà rốt",
            "Khoai tây",
            "Cà chua",
            "Hành tây",
            "Hành lá",
            "Tỏi",
            "Gừng",
            "Nấm",
            "Đậu hũ",
            "Rau cải",
            "Ớt",
            "Nước mắm",
            "Đường",
            "Muối",
            "Tiêu",
            "Dầu ăn",
            "Nước tương",
            "Bột nêm"
        };

        var units = new[]
        {
            "g",
            "kg",
            "ml",
            "l",
            "muỗng",
            "thìa",
            "củ",
            "quả"
        };

        var numberToCreate =
            RequiredRecipes - existingRecipes;

        for (var index = 0;
             index < numberToCreate;
             index++)
        {
            var recipeNumber =
                existingRecipes + index + 1;

            var category =
                categories[
                    index % categories.Count];

            var recipe = new Recipe
            {
                Title =
                    $"Công thức #{recipeNumber:00}",

                Slug =
                    $"lab02-recipe-{recipeNumber}",

                Description =
                    RecipeDescription(category.Name),

                Instructions =
                    "Thực hiện theo các bước chế biến bên dưới.",

                PrepTime =
                    faker.Random.Int(5, 60),

                CookTime =
                    faker.Random.Int(10, 120),

                Servings =
                    faker.Random.Int(1, 8),

                Difficulty =
                    faker.PickRandom<RecipeDifficulty>(),

                Status =
                    RecipeStatus.Published,

                CategoryId =
                    category.Id,

                AuthorId =
                    author.Id,

                PublishedAt =
                    DateTime.UtcNow.AddDays(
                        -faker.Random.Int(0, 180)),

                Nutrition = new RecipeNutrition
                {
                    Calories =
                        faker.Random.Decimal(
                            100,
                            900),

                    Protein =
                        faker.Random.Decimal(
                            5,
                            80),

                    Carbs =
                        faker.Random.Decimal(
                            5,
                            120),

                    Fat =
                        faker.Random.Decimal(
                            1,
                            60)
                }
            };

            // =============================================
            // 10 INGREDIENTS / RECIPE
            // =============================================

            var selectedIngredients =
                faker.PickRandom(
                    ingredientNames,
                    IngredientsPerRecipe)
                .ToList();

            for (var ingredientIndex = 0;
                 ingredientIndex <
                 IngredientsPerRecipe;
                 ingredientIndex++)
            {
                recipe.Ingredients.Add(
                    new RecipeIngredient
                    {
                        Name =
                            selectedIngredients[
                                ingredientIndex],

                        Quantity =
                            Math.Round(
                                faker.Random.Decimal(
                                    1,
                                    500),
                                2),

                        Unit =
                            faker.PickRandom(units),

                        Notes =
                            ingredientIndex % 4 == 0
                                ? "Điều chỉnh theo khẩu vị"
                                : null,

                        OrderIndex =
                            ingredientIndex + 1
                    });
            }

            // =============================================
            // 5 STEPS / RECIPE
            // =============================================

            for (var stepNumber = 1;
                 stepNumber <= StepsPerRecipe;
                 stepNumber++)
            {
                recipe.Steps.Add(
                    new RecipeStep
                    {
                        StepNumber =
                            stepNumber,

                        Title =
                            $"Bước {stepNumber}",

                        Description =
                            StepDescriptions[stepNumber - 1],

                        TimerMinutes =
                            faker.Random.Int(2, 20)
                    });
            }

            context.Recipes.Add(recipe);
        }

        await context.SaveChangesAsync();

        var finalRecipeCount =
            await context.Recipes
                .IgnoreQueryFilters()
                .CountAsync();

        Console.WriteLine(
            $"Recipes: {finalRecipeCount}");

        Console.WriteLine(
            $"Created {numberToCreate} recipes.");

        Console.WriteLine(
            $"Each generated recipe contains " +
            $"{IngredientsPerRecipe} ingredients.");

        Console.WriteLine(
            $"Each generated recipe contains " +
            $"{StepsPerRecipe} steps.");
    }
}