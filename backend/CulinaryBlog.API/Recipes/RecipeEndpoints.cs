using System.Security.Claims;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CulinaryBlog.API.Recipes;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        app.MapPut("/api/v1/recipes/{id:guid}", async (Guid id, UpdateRecipeRequest request,
            ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException();

            return Results.Ok(await sender.Send(new UpdateRecipeCommand(id, request, userId,
                user.IsInRole("Admin")), cancellationToken));
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));

        app.MapPost("/api/v1/recipes", async (CreateRecipeRequest request, ClaimsPrincipal user,
            ISender sender, CancellationToken cancellationToken) =>
        {
            var authorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(authorId))
                throw new UnauthorizedAccessException();

            var recipe = await sender.Send(new CreateRecipeCommand(request, authorId), cancellationToken);
            return Results.Created($"/api/v1/recipes/{recipe.Slug}", recipe);
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));
    }

}
