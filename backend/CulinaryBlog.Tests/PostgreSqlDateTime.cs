namespace CulinaryBlog.Tests;

internal static class PostgreSqlDateTime
{
    public static DateTime? Normalize(DateTime? value) => value is { } timestamp
        ? new DateTime(timestamp.Ticks - timestamp.Ticks % 10, timestamp.Kind) : null;
}
