using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands;

public sealed record AdminUserDto(string Id, string FullName, string Email, string UserName,
    IReadOnlyList<string> Roles, bool EmailConfirmed, DateTime CreatedAt);

public sealed record GetUsersQuery(int Page = 1, int PageSize = 12, string? Search = null, string? Role = null)
    : IRequest<PagedResult<AdminUserDto>>;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(100);
        RuleFor(query => query.Role).Must(role => string.IsNullOrWhiteSpace(role) ||
            role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Author", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class GetUsersQueryHandler(IIdentityService identity) : IRequestHandler<GetUsersQuery, PagedResult<AdminUserDto>>
{
    public async Task<PagedResult<AdminUserDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : "Author";
        var users = await identity.GetUsersAsync(query.Page, query.PageSize, query.Search, role, cancellationToken);
        return new(users.Items.Select(user => new AdminUserDto(user.Id, user.FullName, user.Email, user.UserName,
            user.Roles, user.EmailConfirmed, user.CreatedAt)).ToArray(), users.TotalCount, users.Page, users.PageSize, users.TotalPages);
    }
}
