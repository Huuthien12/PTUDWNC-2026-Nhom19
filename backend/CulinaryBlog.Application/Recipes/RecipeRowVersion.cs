namespace CulinaryBlog.Application.Recipes;

// Canonical Recipe RowVersion representation is Base64 (same rule as the lifecycle commands).
public static class RecipeRowVersion
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length > 0 && Convert.ToBase64String(bytes) == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static byte[] Decode(string value) => Convert.FromBase64String(value);

    public static string Encode(byte[] value) => Convert.ToBase64String(value);
}
