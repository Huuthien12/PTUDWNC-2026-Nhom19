namespace CulinaryBlog.Domain.Entities;

public sealed class SearchHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
}
