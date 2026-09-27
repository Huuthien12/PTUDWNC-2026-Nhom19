using System.Security.Claims;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.DTOs;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
                return Results.Problem(type: "AUTH_TOKEN_INVALID", title: "A user identifier is required.", statusCode: 401);
            try
            {
                return Results.Ok(await sender.Send(new UpdateRecipeCommand(id, request, userId,
                    user.IsInRole("Admin")), cancellationToken));
            }
            catch (RecipeNotFoundException)
            {
                return Results.Problem(type: "RECIPE_NOT_FOUND", title: "Recipe not found.", statusCode: 404);
            }
            catch (RecipeForbiddenException)
            {
                return Results.Problem(type: "RECIPE_FORBIDDEN", title: "Only the author or an Admin may update this recipe.", statusCode: 403);
            }
            catch (ValidationException exception)
            {
                return Results.ValidationProblem(exception.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()),
                    statusCode: 400, title: "Validation failed.", type: "VALIDATION_ERROR");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(type: "RECIPE_CONCURRENCY_CONFLICT",
                    title: "Recipe has changed. Reload it before updating.", statusCode: 422);
            }
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));

        app.MapPost("/api/v1/recipes", async (CreateRecipeRequest request, ClaimsPrincipal user,
            ISender sender, CancellationToken cancellationToken) =>
        {
            var authorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(authorId))
                return Results.Problem(type: "AUTH_TOKEN_INVALID", title: "A user identifier is required.", statusCode: 401);
            try
            {
                var recipe = await sender.Send(new CreateRecipeCommand(request, authorId), cancellationToken);
                return Results.Created($"/api/v1/recipes/{recipe.Slug}", recipe);
            }
            catch (ValidationException exception)
            {
                return Results.ValidationProblem(exception.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()),
                    statusCode: 400, title: "Validation failed.", type: "VALIDATION_ERROR");
            }
            catch (RecipeSlugExistsException) { return SlugConflict(); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Recipes_Slug" })
            {
                // The unique index is authoritative when concurrent requests pass the precheck.
                return SlugConflict();
            }
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));
    }

    private static IResult SlugConflict() => Results.Problem(type: "RECIPE_SLUG_EXISTS",
        title: "Recipe slug already exists.", statusCode: 409);
}
