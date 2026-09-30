using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IdentityRegistrationResult> RegisterAsync(
        string fullName,
        string email,
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await _userManager.FindByEmailAsync(email) is not null)
            return new(false, null, "AUTH_EMAIL_EXISTS");

        if (await _userManager.FindByNameAsync(userName) is not null)
            return new(false, null, "VALIDATION_ERROR");

        const string authorRole = "Author";
        if (!await _roleManager.RoleExistsAsync(authorRole))
        {
            var roleCreated = await _roleManager.CreateAsync(new IdentityRole(authorRole));
            if (!roleCreated.Succeeded)
                return new(false, null, "REGISTRATION_FAILED");
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            var duplicateEmail = created.Errors.Any(error => error.Code == "DuplicateEmail");
            return new(false, null, duplicateEmail ? "AUTH_EMAIL_EXISTS" : "VALIDATION_ERROR");
        }

        var roleAssigned = await _userManager.AddToRoleAsync(user, authorRole);
        if (!roleAssigned.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new(false, null, "REGISTRATION_FAILED");
        }

        return new(true, new IdentityLoginResult(
            true,
            user.Id,
            user.Email,
            user.FullName,
            [authorRole],
            null,
            user.UserName,
            user.AvatarUrl), null);
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
            null,
            user.UserName,
            user.AvatarUrl);
    }

    public async Task<IdentityLoginResult> FindActiveUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
            return new(false, null, null, null, Array.Empty<string>(), "AUTH_ACCOUNT_DISABLED");

        return new(true, user.Id, user.Email, user.FullName,
            (await _userManager.GetRolesAsync(user)).ToArray(), null, user.UserName, user.AvatarUrl);
    }
}
