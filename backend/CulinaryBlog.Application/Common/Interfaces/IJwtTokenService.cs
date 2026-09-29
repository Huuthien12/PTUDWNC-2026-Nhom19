namespace CulinaryBlog.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<AccessTokenResult> GenerateAccessTokenAsync(
        IdentityLoginResult loginResult);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
