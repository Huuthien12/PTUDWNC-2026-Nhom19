using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IRecipeCacheInvalidator? _recipeCache;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IRecipeCacheInvalidator? recipeCache = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _recipeCache = recipeCache;
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

        if (!await _userManager.GetLockoutEnabledAsync(user))
            await _userManager.SetLockoutEnabledAsync(user, true);

        if (await _userManager.IsLockedOutAsync(user))
            return new(false, null, null, null, Array.Empty<string>(), "AUTH_ACCOUNT_LOCKED",
                LockoutEnd: await _userManager.GetLockoutEndDateAsync(user));

        var passwordValid =
            await _userManager.CheckPasswordAsync(
                user,
                password);

        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            var locked = await _userManager.IsLockedOutAsync(user);
            return new IdentityLoginResult(
                false,
                null,
                null,
                null,
                Array.Empty<string>(),
                locked ? "AUTH_ACCOUNT_LOCKED" : "INVALID_CREDENTIALS",
                LockoutEnd: locked ? await _userManager.GetLockoutEndDateAsync(user) : null);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

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

    public async Task<IdentityLoginResult> GoogleLoginAsync(
        string providerKey,
        string email,
        string fullName,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var login = new UserLoginInfo("Google", providerKey, "Google");
        var user = await _userManager.FindByLoginAsync(login.LoginProvider, login.ProviderKey)
            ?? await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            const string authorRole = "Author";
            if (!await _roleManager.RoleExistsAsync(authorRole) &&
                !(await _roleManager.CreateAsync(new IdentityRole(authorRole))).Succeeded)
                return Failed();

            user = new ApplicationUser
            {
                UserName = $"google-{providerKey}", Email = email, EmailConfirmed = true,
                FullName = fullName, AvatarUrl = avatarUrl, IsActive = true, CreatedAt = DateTime.UtcNow
            };
            if (!(await _userManager.CreateAsync(user)).Succeeded ||
                !(await _userManager.AddToRoleAsync(user, authorRole)).Succeeded)
                return Failed();
        }

        if (!user.IsActive || await _userManager.IsLockedOutAsync(user))
            return Failed();
        if ((await _userManager.GetLoginsAsync(user)).All(existing =>
                existing.LoginProvider != login.LoginProvider || existing.ProviderKey != login.ProviderKey) &&
            !(await _userManager.AddLoginAsync(user, login)).Succeeded)
            return Failed();

        return new(true, user.Id, user.Email, user.FullName,
            (await _userManager.GetRolesAsync(user)).ToArray(), null, user.UserName, user.AvatarUrl);
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

    public async Task<UserProfileResult?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId);
        return user is null ? null : await ToProfileAsync(user);
    }

    public async Task<UserProfileResult?> UpdateProfileAsync(string userId, string? fullName, string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;
        if (fullName is not null) user.FullName = fullName.Trim();
        if (avatarUrl is not null) user.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl;
        if (!(await _userManager.UpdateAsync(user)).Succeeded) return null;
        // Identity's UpdateAsync has completed its SaveChanges before invalidation.
        if (_recipeCache is not null) await _recipeCache.AuthorChangedAsync(userId, cancellationToken);
        return await ToProfileAsync(user);
    }

    public async Task<IdentityPasswordChangeResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
            return new(false, "AUTH_ACCOUNT_DISABLED");

        if (!await _userManager.GetLockoutEnabledAsync(user))
            await _userManager.SetLockoutEnabledAsync(user, true);
        if (await _userManager.IsLockedOutAsync(user))
            return new(false, "AUTH_ACCOUNT_LOCKED", await _userManager.GetLockoutEndDateAsync(user));

        if (!await _userManager.CheckPasswordAsync(user, currentPassword))
        {
            await _userManager.AccessFailedAsync(user);
            var locked = await _userManager.IsLockedOutAsync(user);
            return new(false, locked ? "AUTH_ACCOUNT_LOCKED" : "INVALID_CURRENT_PASSWORD",
                locked ? await _userManager.GetLockoutEndDateAsync(user) : null);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var changed = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return changed.Succeeded ? new(true, null) : new(false, "VALIDATION_ERROR");
    }

    private async Task<UserProfileResult> ToProfileAsync(ApplicationUser user) => new(
        user.Id, user.FullName, user.Email!, user.UserName!, user.AvatarUrl,
        (await _userManager.GetRolesAsync(user)).ToArray(), user.EmailConfirmed, user.CreatedAt);

    private static IdentityLoginResult Failed() => new(false, null, null, null, Array.Empty<string>(), "AUTH_ACCOUNT_DISABLED");
}
