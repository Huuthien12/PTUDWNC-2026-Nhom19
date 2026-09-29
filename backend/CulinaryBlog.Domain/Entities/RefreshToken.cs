using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RefreshToken : BaseEntity
{
    // SHA-256 hex digest only; the raw refresh token is returned to the client once.
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    // SHA-256 digest of the replacement token, never the raw secret.
    public string? ReplacedByToken { get; set; }

    // Foreign Key kết nối với ApplicationUser
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
