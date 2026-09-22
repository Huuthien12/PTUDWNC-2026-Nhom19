using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IdentityLoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return new IdentityLoginResult(
                false,
                null,
                null,
                null,
                Array.Empty<string>(),
                "INVALID_CREDENTIALS");
        }

        if (!user.IsActive)
        {
            return new IdentityLoginResult(
                false,
                null,
                null,
                null,
                Array.Empty<string>(),
                "USER_INACTIVE");
        }

        var passwordValid =
            await _userManager.CheckPasswordAsync(
                user,
                password);

        if (!passwordValid)
        {
            return new IdentityLoginResult(
                false,
                null,
                null,
                null,
                Array.Empty<string>(),
                "INVALID_CREDENTIALS");
        }

        var roles =
    (await _userManager.GetRolesAsync(user)).ToArray();

        return new IdentityLoginResult(
            true,
            user.Id,
            user.Email,
            user.FullName,
            roles,
            null);
    }
}