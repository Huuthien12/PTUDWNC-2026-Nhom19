namespace CulinaryBlog.Application.Common.Interfaces;

public interface IRecipeCacheInvalidator
{
    Task RecipeChangedAsync(string slug, CancellationToken cancellationToken);
    Task CategoryChangedAsync(Guid categoryId, CancellationToken cancellationToken);
    Task AuthorChangedAsync(string authorId, CancellationToken cancellationToken);
    Task ListChangedAsync(CancellationToken cancellationToken);
    Task<string?> GetSuggestionVersionAsync(CancellationToken cancellationToken);
    Task InvalidateSuggestionsAsync(CancellationToken cancellationToken);
}
