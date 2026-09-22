namespace CulinaryBlog.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<string> GenerateAccessTokenAsync(
        IdentityLoginResult loginResult);
}
