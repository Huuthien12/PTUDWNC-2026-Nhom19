namespace CulinaryBlog.Tests;

internal static class PostgreSqlDateTime
{
    public static DateTime Normalize(DateTime value) => new(value.Ticks / 10 * 10, value.Kind);
    public static DateTime? Normalize(DateTime? value) => value is null ? null : Normalize(value.Value);
}
