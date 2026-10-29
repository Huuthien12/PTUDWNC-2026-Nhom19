using CulinaryBlog.Application.Authentication.Commands;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CulinaryBlog.API.Authentication;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/users", async (int? page, int? pageSize, string? search, string? role,
            ISender sender, CancellationToken cancellationToken) => Results.Ok(await sender.Send(
                new GetUsersQuery(page ?? 1, pageSize ?? 12, search, role), cancellationToken)))
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser().RequireRole("Admin"));
    }
}
