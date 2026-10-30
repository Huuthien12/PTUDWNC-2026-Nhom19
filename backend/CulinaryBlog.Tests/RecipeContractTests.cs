using System.Text.Json;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Validators;
using CulinaryBlog.Domain.Enums;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeContractTests
{
    private static CreateRecipeRequest Create() => new(
        "Canh chua", "", Guid.NewGuid(), 1, 1, 1, RecipeDifficulty.Easy);

    private static UpdateRecipeRequest Update() => new(
        "Canh chua", "", Guid.NewGuid(), 1, 0, 1, RecipeDifficulty.Easy, "", "AQ==");

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Title_boundaries_apply_to_both_requests(int length, bool valid)
    {
        Assert.Equal(valid, new CreateRecipeRequestValidator().Validate(Create() with { Title = new string('a', length) }).IsValid);
        Assert.Equal(valid, new UpdateRecipeRequestValidator().Validate(Update() with { Title = new string('a', length) }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void Missing_or_blank_title_is_invalid(string? title)
    {
        Assert.False(new CreateRecipeRequestValidator().Validate(Create() with { Title = title! }).IsValid);
        Assert.False(new UpdateRecipeRequestValidator().Validate(Update() with { Title = title! }).IsValid);
    }

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(0, false, true)]
    [InlineData(1, true, true)]
    [InlineData(int.MaxValue, true, true)]
    public void Cook_time_respects_each_contract(int value, bool createValid, bool updateValid)
    {
        Assert.Equal(createValid, new CreateRecipeRequestValidator().Validate(Create() with { CookTime = value }).IsValid);
        Assert.Equal(updateValid, new UpdateRecipeRequestValidator().Validate(Update() with { CookTime = value }).IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public void Prep_time_and_servings_require_positive_values(int value, bool valid)
    {
        var create = new CreateRecipeRequestValidator();
        var update = new UpdateRecipeRequestValidator();
        Assert.Equal(valid, create.Validate(Create() with { PrepTime = value }).IsValid);
        Assert.Equal(valid, create.Validate(Create() with { Servings = value }).IsValid);
        Assert.Equal(valid, update.Validate(Update() with { PrepTime = value }).IsValid);
        Assert.Equal(valid, update.Validate(Update() with { Servings = value }).IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void Difficulty_must_be_defined(int value, bool valid)
    {
        Assert.Equal(valid, new CreateRecipeRequestValidator().Validate(Create() with { Difficulty = (RecipeDifficulty)value }).IsValid);
        Assert.Equal(valid, new UpdateRecipeRequestValidator().Validate(Update() with { Difficulty = (RecipeDifficulty)value }).IsValid);
    }

    [Fact]
    public void Category_requires_nonempty_guid_but_does_not_query_existence()
    {
        Assert.True(new CreateRecipeRequestValidator().Validate(Create()).IsValid);
        Assert.True(new UpdateRecipeRequestValidator().Validate(Update()).IsValid);
        Assert.False(new CreateRecipeRequestValidator().Validate(Create() with { CategoryId = Guid.Empty }).IsValid);
        Assert.False(new UpdateRecipeRequestValidator().Validate(Update() with { CategoryId = Guid.Empty }).IsValid);
    }

    public static IEnumerable<object?[]> NutritionCases()
    {
        yield return [null, null];
        yield return [new RecipeNutritionDto(), null];
        yield return [new RecipeNutritionDto(0, 0, 0, 0), null];
        yield return [new RecipeNutritionDto(0.001m, 1.25m, null, decimal.MaxValue), null];
        yield return [new RecipeNutritionDto(Calories: -0.001m), "Nutrition.Calories"];
        yield return [new RecipeNutritionDto(Protein: -0.001m), "Nutrition.Protein"];
        yield return [new RecipeNutritionDto(Carbs: -0.001m), "Nutrition.Carbs"];
        yield return [new RecipeNutritionDto(Fat: -0.001m), "Nutrition.Fat"];
    }

    [Theory]
    [MemberData(nameof(NutritionCases))]
    public void Optional_nutrition_validates_each_supplied_value(RecipeNutritionDto? nutrition, string? errorField)
    {
        var results = new[] {
            new CreateRecipeRequestValidator().Validate(Create() with { Nutrition = nutrition }),
            new UpdateRecipeRequestValidator().Validate(Update() with { Nutrition = nutrition })
        };
        foreach (var result in results)
        {
            Assert.Equal(errorField is null, result.IsValid);
            if (errorField is not null) Assert.Equal(errorField, Assert.Single(result.Errors).PropertyName);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("not-base64", false)]
    [InlineData("AQ", false)]
    [InlineData("AQ==\n", false)]
    [InlineData("AR==", false)]
    [InlineData("\"AQ==\"", false)]
    [InlineData("AQ==", true)]
    [InlineData("AQIDBA==", true)]
    public void Row_version_is_opaque_canonical_base64_without_fixed_length(string? value, bool valid)
    {
        var result = new UpdateRecipeRequestValidator().Validate(Update() with { RowVersion = value! });
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("RowVersion", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Text_nullability_matches_model_and_optional_create_instructions()
    {
        Assert.True(new CreateRecipeRequestValidator().Validate(Create() with { Instructions = null }).IsValid);
        Assert.False(new CreateRecipeRequestValidator().Validate(Create() with { Description = null! }).IsValid);
        Assert.False(new UpdateRecipeRequestValidator().Validate(Update() with { Description = null! }).IsValid);
        Assert.False(new UpdateRecipeRequestValidator().Validate(Update() with { Instructions = null! }).IsValid);
    }

    [Fact]
    public void Web_json_contract_uses_model_names_and_excludes_server_owned_fields()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var request in new object[] { Create(), Update() })
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(request, options));
            Assert.True(json.RootElement.TryGetProperty("prepTime", out _));
            Assert.True(json.RootElement.TryGetProperty("cookTime", out _));
            Assert.Equal(1, json.RootElement.GetProperty("difficulty").GetInt32());
            foreach (var field in new[] { "authorId", "status", "slug", "prepTimeMinutes", "cookTimeMinutes" })
                Assert.False(json.RootElement.TryGetProperty(field, out _));
        }
        var response = new RecipeDto(Guid.NewGuid(), "Canh chua", "canh-chua", "", "",
            Guid.NewGuid(), "author", 1, 1, 1, RecipeDifficulty.Easy, RecipeStatus.Draft,
            new RecipeNutritionDto(0), "AQ==", DateTime.UtcNow, DateTime.UtcNow, null);
        var serialized = JsonSerializer.Serialize(response, options);
        Assert.Equal(response, JsonSerializer.Deserialize<RecipeDto>(serialized, options));
        using var responseJson = JsonDocument.Parse(serialized);
        Assert.Equal("AQ==", responseJson.RootElement.GetProperty("rowVersion").GetString());
        Assert.Equal(0, responseJson.RootElement.GetProperty("nutrition").GetProperty("calories").GetDecimal());
    }
}
