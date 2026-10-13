using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Mappers;
using CulinaryBlog.Application.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeDetailCacheTests
{
    private static readonly GetRecipeDetailQuery Query = new("requested-slug", "owner", false);

    private static Recipe Published() => new()
    {
        Id = Guid.NewGuid(), Slug = "stored-slug", Title = "Fresh detail",
        Status = RecipeStatus.Published, RowVersion = [1, 2, 3], AuthorId = "owner",
        Category = new Category(), Author = new ApplicationUser { Id = "owner" }
    };

    private static (FakeRecipeRepository Repository, FakeCache Cache) Fakes(Recipe recipe) =>
        (new FakeRecipeRepository
        {
            Access = new(recipe.Id, recipe.Slug, recipe.Status, recipe.RowVersion.ToArray()),
            Detail = recipe
        }, new FakeCache());

    private static GetRecipeDetailQueryHandler Handler(FakeRecipeRepository repository, FakeCache cache,
        RecipeDetailCacheOptions? options = null) => new(repository, cache,
            Options.Create(options ?? new RecipeDetailCacheOptions { Enabled = true }));

    [Fact]
    public async Task Explicitly_disabled_uses_only_the_existing_detail_query()
    {
        var (repository, cache) = Fakes(Published());
        repository.Access = null;

        var result = await Handler(repository, cache, new RecipeDetailCacheOptions { Enabled = false }).Handle(Query, default);

        Assert.Equal(repository.Detail!.Id, result.Id);
        Assert.Equal(0, repository.AccessCalls);
        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(0, cache.ReadCalls);
        Assert.Equal(0, cache.WriteCalls);
        Assert.Equal(Query, repository.LastDetailQuery);
    }

    [Fact]
    public async Task Matching_published_hit_skips_the_full_detail_query()
    {
        var (repository, cache) = Fakes(Published());
        cache.Cached = RecipeDetailMapper.ToDto(repository.Detail!);

        var result = await Handler(repository, cache).Handle(Query, default);

        Assert.Same(cache.Cached, result);
        Assert.Equal(1, repository.AccessCalls);
        Assert.Equal(Query, repository.LastAccessQuery);
        Assert.Equal(0, repository.DetailCalls);
        Assert.Equal("recipe:stored-slug", cache.ReadKey);
        Assert.Equal(0, cache.WriteCalls);
    }

    [Fact]
    public async Task Miss_reads_authorized_detail_and_writes_absolute_five_minutes()
    {
        var (repository, cache) = Fakes(Published());

        var result = await Handler(repository, cache).Handle(Query, default);

        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(Query, repository.LastDetailQuery);
        Assert.Equal("recipe:stored-slug", cache.ReadKey);
        Assert.Equal("recipe:stored-slug", cache.WriteKey);
        Assert.Equal(TimeSpan.FromMinutes(5), cache.Expiration);
        Assert.True(cache.UseAbsoluteExpiration);
        Assert.Equal(1, cache.WriteCalls);
        Assert.Same(result, cache.Written);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("id")]
    [InlineData("slug")]
    [InlineData("status")]
    [InlineData("malformed-version")]
    public async Task Mismatching_hit_is_replaced_by_fresh_detail(string mismatch)
    {
        var (repository, cache) = Fakes(Published());
        cache.Cached = RecipeDetailMapper.ToDto(repository.Detail!);
        cache.Cached.Title = "Stale detail";
        switch (mismatch)
        {
            case "version": cache.Cached.RowVersion = Convert.ToBase64String([9, 9, 9]); break;
            case "id": cache.Cached.Id = Guid.NewGuid(); break;
            case "slug": cache.Cached.Slug = "wrong-slug"; break;
            case "status": cache.Cached.Status = RecipeStatus.Draft; break;
            default: cache.Cached.RowVersion = "invalid-base64"; break;
        }

        var result = await Handler(repository, cache).Handle(Query, default);

        Assert.Equal("Fresh detail", result.Title);
        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(1, cache.WriteCalls);
        Assert.Same(result, cache.Written);
        Assert.Equal(Convert.ToBase64String(repository.Access!.RowVersion), result.RowVersion);
    }

    [Fact]
    public async Task Invisible_or_deleted_recipe_returns_not_found_without_reading_existing_cache()
    {
        var (repository, cache) = Fakes(Published());
        cache.Cached = RecipeDetailMapper.ToDto(repository.Detail!);
        repository.Access = null; // Repository contract for deleted, missing, or unauthorized recipes.

        await Assert.ThrowsAsync<NotFoundException>(() => Handler(repository, cache).Handle(Query, default));

        Assert.Equal(1, repository.AccessCalls);
        Assert.Equal(0, repository.DetailCalls);
        Assert.Equal(0, cache.ReadCalls);
        Assert.Equal(0, cache.WriteCalls);
    }

    [Theory]
    [InlineData(RecipeStatus.Draft)]
    [InlineData(RecipeStatus.Archived)]
    public async Task Authorized_private_recipe_bypasses_cache(RecipeStatus status)
    {
        var recipe = Published();
        recipe.Status = status;
        var (repository, cache) = Fakes(recipe);

        var result = await Handler(repository, cache).Handle(Query, default);

        Assert.Equal(status, result.Status);
        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(Query, repository.LastDetailQuery);
        Assert.Equal(0, cache.ReadCalls);
        Assert.Equal(0, cache.WriteCalls);
    }

    [Theory]
    [InlineData(RecipeStatus.Draft)]
    [InlineData(RecipeStatus.Archived)]
    public async Task Status_change_between_reads_does_not_cache_private_detail(RecipeStatus status)
    {
        var (repository, cache) = Fakes(Published());
        repository.Detail!.Status = status; // Access snapshot remains Published.

        var result = await Handler(repository, cache).Handle(Query, default);

        Assert.Equal(status, result.Status);
        Assert.Equal(1, repository.AccessCalls);
        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(Query, repository.LastDetailQuery);
        Assert.Equal(1, cache.ReadCalls);
        Assert.Equal(0, cache.WriteCalls);
    }

    [Fact]
    public async Task Full_read_becoming_invisible_returns_not_found_without_writing_cache()
    {
        var (repository, cache) = Fakes(Published());
        repository.Detail = null;

        await Assert.ThrowsAsync<NotFoundException>(() => Handler(repository, cache).Handle(Query, default));

        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(0, cache.WriteCalls);
    }

    [Fact]
    public async Task Full_read_error_is_not_cached()
    {
        var (repository, cache) = Fakes(Published());
        repository.DetailError = new InvalidOperationException("Test-only repository failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(repository, cache).Handle(Query, default));

        Assert.Equal(1, repository.DetailCalls);
        Assert.Equal(0, cache.WriteCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cache_transport_failure_falls_back_to_authorized_database(bool readFailure)
    {
        var (repository, cache) = Fakes(Published());
        if (readFailure) cache.ReadError = new IOException();
        else cache.WriteError = new TimeoutException();
        var result = await Handler(repository, cache).Handle(Query, default);
        Assert.Equal(repository.Detail!.Id, result.Id);
        Assert.Equal(Query, repository.LastDetailQuery);
        Assert.Equal(1, repository.DetailCalls);
    }

    [Fact]
    public async Task Cache_fallback_does_not_swallow_database_or_permission_failure()
    {
        var (repository, cache) = Fakes(Published());
        cache.ReadError = new IOException();
        repository.DetailError = new NotFoundException("RECIPE_NOT_FOUND", "Hidden");
        await Assert.ThrowsAsync<NotFoundException>(() => Handler(repository, cache).Handle(Query, default));
        repository.DetailError = new InvalidOperationException("Database failure");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(repository, cache).Handle(Query, default));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cache_cancellation_and_programming_errors_propagate(bool cancellation)
    {
        var (repository, cache) = Fakes(Published());
        cache.ReadError = cancellation ? new OperationCanceledException() : new InvalidOperationException();
        if (cancellation)
            await Assert.ThrowsAsync<OperationCanceledException>(() => Handler(repository, cache).Handle(Query, default));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(repository, cache).Handle(Query, default));
    }

    private sealed class FakeCache : ICacheService
    {
        public Exception? ReadError { get; set; }
        public Exception? WriteError { get; set; }
        public RecipeDetailDto? Cached { get; set; }
        public RecipeDetailDto? Written { get; private set; }
        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }
        public string? ReadKey { get; private set; }
        public string? WriteKey { get; private set; }
        public TimeSpan Expiration { get; private set; }
        public bool UseAbsoluteExpiration { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (ReadError is not null) throw ReadError;
            ReadCalls++;
            ReadKey = key;
            return Task.FromResult(Cached is null ? default : (T?)(object)Cached);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration,
            CancellationToken cancellationToken = default, bool useAbsoluteExpiration = false)
        {
            if (WriteError is not null) throw WriteError;
            WriteCalls++;
            WriteKey = key;
            Written = (RecipeDetailDto)(object)value!;
            Expiration = expiration;
            UseAbsoluteExpiration = useAbsoluteExpiration;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public RecipeDetailAccessResult? Access { get; set; }
        public Recipe? Detail { get; set; }
        public Exception? DetailError { get; set; }
        public int AccessCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public GetRecipeDetailQuery? LastAccessQuery { get; private set; }
        public GetRecipeDetailQuery? LastDetailQuery { get; private set; }

        public Task<RecipeDetailAccessResult?> GetDetailAccessBySlugAsync(string slug, string? userId,
            bool isAdmin, CancellationToken cancellationToken = default)
        {
            AccessCalls++;
            LastAccessQuery = new(slug, userId, isAdmin);
            return Task.FromResult(Access);
        }

        public Task<Recipe?> GetBySlugAsync(string slug, string? userId, bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            LastDetailQuery = new(slug, userId, isAdmin);
            if (DetailError is not null) throw DetailError;
            return Task.FromResult(Detail);
        }

        // Fail immediately if the handler uses an unrelated repository operation.
        public Task<int> CountVisibleAsync(string? userId, bool isAdmin, Guid? categoryId, RecipeDifficulty? difficulty, int? maxCookTime, int? minServings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetVisibleAsync(int page, int pageSize, string? userId, bool isAdmin, Guid? categoryId, RecipeDifficulty? difficulty, int? maxCookTime, int? minServings, string sortBy, string sortOrder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Recipe?> GetForLifecycleAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddStepAsync(RecipeStep step, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AddIngredient(RecipeIngredient ingredient) => throw new NotSupportedException();
        public void Update(Recipe recipe, byte[] originalRowVersion) => throw new NotSupportedException();
        public void UpdateForChildMutation(Recipe recipe, byte[] originalRowVersion) => throw new NotSupportedException();
        public Task<int> CountSearchAsync(string tsQuery, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SearchRecipeResult>> SearchAsync(string tsQuery, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, Guid? excludeRecipeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountByCategoryAsync(Guid categoryId, string? userId, bool isAdmin, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetByCategoryAsync(Guid categoryId, int page, int pageSize, string? userId, bool isAdmin, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountPublishedByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountAllByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Recipe?> GetForImagesAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AddImage(RecipeImage image) => throw new NotSupportedException();
        public void RemoveImage(RecipeImage image) => throw new NotSupportedException();
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Recipe?> GetForStepMutationAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecipeSuggestionDto>> GetSuggestionsAsync(string normalizedQuery, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
