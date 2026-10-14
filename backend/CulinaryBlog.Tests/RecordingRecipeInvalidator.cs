using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Tests;

public sealed class RecordingRecipeInvalidator : IRecipeCacheInvalidator
{
    public List<string> Authors { get; } = [];
    public Task RecipeChangedAsync(string slug, CancellationToken token) => Task.CompletedTask;
    public Task CategoryChangedAsync(Guid id, CancellationToken token) => Task.CompletedTask;
    public Task AuthorChangedAsync(string id, CancellationToken token) { Authors.Add(id); return Task.CompletedTask; }
    public Task ListChangedAsync(CancellationToken token) => Task.CompletedTask;
}
