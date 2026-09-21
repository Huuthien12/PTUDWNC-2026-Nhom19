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

    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        // Cố định seed để dữ liệu có thể tái tạo.
        Randomizer.Seed = new Random(2312753);

        Console.WriteLine("===== LAB 02 DATA SEED =====");

        var author = await SeedAuthorAsync(userManager);

        var categories = await SeedCategoriesAsync(context);

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
                $"Danh mục dữ liệu mẫu Lab 02 - {name}");

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
                    $"Công thức Lab 02 số {recipeNumber}",

                Slug =
                    $"lab02-recipe-{recipeNumber}",

                Description =
                    faker.Lorem.Sentences(2),

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
                            faker.Lorem.Sentences(2),

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