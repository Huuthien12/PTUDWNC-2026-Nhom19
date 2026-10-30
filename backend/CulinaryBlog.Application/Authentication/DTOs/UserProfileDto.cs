namespace CulinaryBlog.Application.Authentication.DTOs;

public sealed record UserProfileDto(
    string Id,
    string FullName,
    string Email,
    string UserName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    DateTime CreatedAt);
