using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CulinaryBlog.SeedUpgradeCli;

public static class SnapshotHashing
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string Json<T>(T value) => JsonSerializer.Serialize(value, CanonicalOptions);

    public static string Hash<T>(T value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Json(value)))).ToLowerInvariant();
}
