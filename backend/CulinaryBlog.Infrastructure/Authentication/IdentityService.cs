using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Infrastructure.Persistence;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IRecipeCacheInvalidator? _recipeCache;
    private readonly AppDbContext _db;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, AppDbContext db, IRecipeCacheInvalidator? recipeCache = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
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

    public async Task<PagedIdentityUsersResult> GetUsersAsync(int page, int pageSize, string? search, string? role,
        CancellationToken cancellationToken = default)
    {
        var users = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            users = users.Where(user => user.UserName!.ToUpper().Contains(term) || user.Email!.ToUpper().Contains(term) ||
                user.FullName.ToUpper().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(role))
            users = users.Where(user => _db.UserRoles.Join(_db.Roles, membership => membership.RoleId,
                assignedRole => assignedRole.Id, (_, assignedRole) => new { assignedRole.Name, _.UserId })
                .Any(item => item.UserId == user.Id && item.Name == role));
        var total = await users.CountAsync(cancellationToken);
        var rows = await users.OrderBy(user => user.UserName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new { user.Id, user.FullName, user.Email, user.UserName, user.EmailConfirmed, user.CreatedAt })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(user => user.Id).ToArray();
        var roles = await _db.UserRoles.Where(membership => ids.Contains(membership.UserId)).Join(_db.Roles,
            membership => membership.RoleId, assignedRole => assignedRole.Id,
            (membership, assignedRole) => new { membership.UserId, assignedRole.Name }).ToListAsync(cancellationToken);
        var items = rows.Select(user => new IdentityUserListItem(user.Id, user.FullName, user.Email ?? string.Empty,
            user.UserName ?? string.Empty, roles.Where(role => role.UserId == user.Id).Select(role => role.Name!).ToArray(),
            user.EmailConfirmed, user.CreatedAt)).ToArray();
        return new(items, total, page, pageSize,
            (int)Math.Ceiling(total / (double)pageSize));
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

        if (await _userManager.IsLockedOutAsync(user))
            return new(false, "AUTH_ACCOUNT_LOCKED", await _userManager.GetLockoutEndDateAsync(user));

        if (!await _userManager.CheckPasswordAsync(user, currentPassword))
            return new(false, "INVALID_CURRENT_PASSWORD");

        var changed = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return changed.Succeeded ? new(true, null) : new(false, "VALIDATION_ERROR");
    }

    public async Task<PasswordResetTokenResult?> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return null;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return new(user.Email, user.FullName, Base64UrlEncoder.Encode(token));
    }

    public async Task<IdentityPasswordResetResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
            return new(false, null, "RESET_PASSWORD_INVALID_OR_EXPIRED");

        string identityToken;
        try
        {
            identityToken = Base64UrlEncoder.Decode(token);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return new(false, null, "RESET_PASSWORD_INVALID_OR_EXPIRED");
        }

        var reset = await _userManager.ResetPasswordAsync(user, identityToken, newPassword);
        if (reset.Succeeded) return new(true, user.Id, null);

        var invalidToken = reset.Errors.Any(error => error.Code == "InvalidToken");
        return new(false, null, invalidToken ? "RESET_PASSWORD_INVALID_OR_EXPIRED" : "VALIDATION_ERROR");
    }

    private async Task<UserProfileResult> ToProfileAsync(ApplicationUser user) => new(
        user.Id, user.FullName, user.Email!, user.UserName!, user.AvatarUrl,
        (await _userManager.GetRolesAsync(user)).ToArray(), user.EmailConfirmed, user.CreatedAt);

    private static IdentityLoginResult Failed() => new(false, null, null, null, Array.Empty<string>(), "AUTH_ACCOUNT_DISABLED");
}
