using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GenerateAccessTokenAsync(
        IdentityLoginResult loginResult)
    {
        if (!loginResult.Succeeded ||
            string.IsNullOrWhiteSpace(loginResult.UserId))
        {
            throw new InvalidOperationException(
                "Cannot generate token for an invalid login result.");
        }

        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT Key is not configured.");

        var jwtIssuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT Issuer is not configured.");

        var jwtAudience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT Audience is not configured.");

        var expirationMinutes =
            _configuration.GetValue<int?>(
                "Jwt:ExpirationMinutes") ?? 60;

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                loginResult.UserId),

            new(
                ClaimTypes.Email,
                loginResult.Email ?? string.Empty),

            new(
                ClaimTypes.Name,
                loginResult.FullName ?? string.Empty),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        foreach (var role in loginResult.Roles)
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var expiresAt =
            DateTime.UtcNow.AddMinutes(
                expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return Task.FromResult(
            new JwtSecurityTokenHandler()
                .WriteToken(token));
    }
}