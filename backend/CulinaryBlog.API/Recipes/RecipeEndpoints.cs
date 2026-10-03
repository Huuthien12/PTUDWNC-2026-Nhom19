using System.Security.Claims;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Queries;
using CulinaryBlog.Application.SearchHistory;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Recipes;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        app.MapDelete(
            "/api/v1/recipes/{id:guid}",
            async (
                Guid id,
                [FromBody] LifecycleRequest request,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userId = user.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                    throw new UnauthorizedAccessException();

                await sender.Send(
                    new DeleteRecipeCommand(
                        id,
                        request.RowVersion,
                        userId,
                        user.IsInRole("Admin")),
                    cancellationToken);

                return Results.NoContent();
            })
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("Author", "Admin"));

        foreach (var action in new[]
        {
            RecipeLifecycleAction.Publish,
            RecipeLifecycleAction.Unpublish,
            RecipeLifecycleAction.Archive
        })
        {
            var path = action switch
            {
                RecipeLifecycleAction.Publish =>
                    "/api/v1/recipes/{id:guid}/publish",

                RecipeLifecycleAction.Unpublish =>
                    "/api/v1/recipes/{id:guid}/unpublish",

                _ =>
                    "/api/v1/recipes/{id:guid}/archive"
            };

            app.MapPatch(
                path,
                async (
                    Guid id,
                    LifecycleRequest request,
                    ClaimsPrincipal user,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var userId = user.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                    if (string.IsNullOrWhiteSpace(userId))
                        throw new UnauthorizedAccessException();

                    return Results.Ok(
                        await sender.Send(
                            new ChangeRecipeLifecycleCommand(
                                id,
                                action,
                                request.RowVersion,
                                userId,
                                user.IsInRole("Admin")),
                            cancellationToken));
                })
                .RequireAuthorization(policy => policy
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireRole("Author", "Admin"));
        }

        app.MapPut(
            "/api/v1/recipes/{id:guid}",
            async (
                Guid id,
                UpdateRecipeRequest request,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userId = user.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                    throw new UnauthorizedAccessException();

                return Results.Ok(
                    await sender.Send(
                        new UpdateRecipeCommand(
                            id,
                            request,
                            userId,
                            user.IsInRole("Admin")),
                        cancellationToken));
            })
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("Author", "Admin"));

        app.MapPost(
            "/api/v1/recipes",
            async (
                CreateRecipeRequest request,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var authorId = user.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(authorId))
                    throw new UnauthorizedAccessException();

                var recipe = await sender.Send(
                    new CreateRecipeCommand(
                        request,
                        authorId),
                    cancellationToken);

                return Results.Created(
                    $"/api/v1/recipes/{recipe.Slug}",
                    recipe);
            })
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("Author", "Admin"));

                app.MapPost(
                    "/api/v1/recipes/{id:guid}/steps",
                    async (
                        Guid id,
                        [FromBody] CreateRecipeStepRequest request,
                        ClaimsPrincipal user,
                        ISender sender,
                        CancellationToken cancellationToken) =>
                    {
                        var userId = user.FindFirstValue(
                            ClaimTypes.NameIdentifier);

                        if (string.IsNullOrWhiteSpace(userId))
                            throw new UnauthorizedAccessException();

                        var result = await sender.Send(
                            new CreateRecipeStepCommand(
                                id,
                                request,
                                userId,
                                user.IsInRole("Admin")),
                            cancellationToken);

                        return Results.Created(
                            $"/api/v1/recipes/{id}/steps/{result.Items.Last().Id}",
                            result);
                    })
                    .RequireAuthorization(policy => policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireRole("Author", "Admin"));

        // GET /api/v1/recipes/suggestions?q={prefix}
        // Public endpoint: Guest / Author / Admin
        app.MapGet(
            "/api/v1/recipes/suggestions",
            async (
                string? q,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetRecipeSuggestionsQuery(
                        q ?? string.Empty),
                    cancellationToken);

                return Results.Ok(result);
            });

        app.MapPut(
            "/api/v1/recipes/{id:guid}/steps/{stepId:guid}",
            async (
                Guid id,
                Guid stepId,
                [FromBody] UpdateRecipeStepRequest request,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userId = user.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                    throw new UnauthorizedAccessException();

                var result = await sender.Send(
                    new UpdateRecipeStepCommand(
                        id,
                        stepId,
                        request,
                        userId,
                        user.IsInRole("Admin")),
                    cancellationToken);

                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireRole("Author", "Admin"));

        app.MapDelete(
            "/api/v1/recipes/{id:guid}/steps/{stepId:guid}",
            async (
                Guid id,
                Guid stepId,
                [FromBody] DeleteRecipeStepRequest request,
                HttpContext context,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userId = user.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                    throw new UnauthorizedAccessException();

                var rowVersion = await sender.Send(
                    new DeleteRecipeStepCommand(
                        id,
                        stepId,
                        request.RowVersion,
                        userId,
                        user.IsInRole("Admin")),
                    cancellationToken);

                context.Response.Headers.ETag = $"\"{rowVersion}\"";
                return Results.NoContent();
            })
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("Author", "Admin"));

        var history = app.MapGroup("/api/v1/search-history").RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Author", "Admin"));
        app.MapPost("/api/v1/recipes/{id:guid}/ingredients", async (Guid id, [FromBody] CreateRecipeIngredientRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) => Results.Created($"/api/v1/recipes/{id}/ingredients", await sender.Send(new AddRecipeIngredientCommand(id, request, user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.IsInRole("Admin")), ct))).RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Author", "Admin"));
        app.MapPut("/api/v1/recipes/{id:guid}/ingredients/{ingId:guid}", async (Guid id, Guid ingId, [FromBody] UpdateRecipeIngredientRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new UpdateRecipeIngredientCommand(id, ingId, request, user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.IsInRole("Admin")), ct))).RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Author", "Admin"));
        app.MapDelete("/api/v1/recipes/{id:guid}/ingredients/{ingId:guid}", async (Guid id, Guid ingId, [FromBody] LifecycleRequest request, HttpContext context, ClaimsPrincipal user, ISender sender, CancellationToken ct) => { var rowVersion = await sender.Send(new DeleteRecipeIngredientCommand(id, ingId, request.RowVersion, user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.IsInRole("Admin")), ct); context.Response.Headers.ETag = $"\"{rowVersion}\""; return Results.NoContent(); }).RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Author", "Admin"));
        history.MapGet("/", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) => Results.Ok(await sender.Send(new GetSearchHistoryQuery(user.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken)));
        history.MapPost("/", async (CreateSearchHistoryRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) => { var item = await sender.Send(new CreateSearchHistoryCommand(request, user.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken); return Results.Created($"/api/v1/search-history/{item.Id}", item); });
        history.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) => { await sender.Send(new DeleteSearchHistoryCommand(id, user.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken); return Results.NoContent(); });
        history.MapDelete("/", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) => { await sender.Send(new ClearSearchHistoryCommand(user.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken); return Results.NoContent(); });
/* Main-side duplicate core mappings retained here only during conflict resolution.
            var recipe = await sender.Send(new CreateRecipeCommand(request, authorId), cancellationToken);
            return Results.Created($"/api/v1/recipes/{recipe.Slug}", recipe);
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));

        app.MapPost("/api/v1/recipes/{id:guid}/ingredients", async (
            Guid id, [FromBody] CreateRecipeIngredientRequest request, ClaimsPrincipal user,
            ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException();
            var ingredient = await sender.Send(new AddRecipeIngredientCommand(
                id, request, userId, user.IsInRole("Admin")), cancellationToken);
            return Results.Created($"/api/v1/recipes/{id}/ingredients/{ingredient.Id}", ingredient);
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));

        app.MapPut("/api/v1/recipes/{id:guid}/ingredients/{ingId:guid}", async (
            Guid id, Guid ingId, [FromBody] UpdateRecipeIngredientRequest request, ClaimsPrincipal user,
            ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException();
            return Results.Ok(await sender.Send(new UpdateRecipeIngredientCommand(
                id, ingId, request, userId, user.IsInRole("Admin")), cancellationToken));
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));

        app.MapDelete("/api/v1/recipes/{id:guid}/ingredients/{ingId:guid}", async (
            Guid id, Guid ingId, [FromBody] LifecycleRequest request, HttpContext context, ClaimsPrincipal user,
            ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException();
            var rowVersion = await sender.Send(new DeleteRecipeIngredientCommand(
                id, ingId, request.RowVersion, userId, user.IsInRole("Admin")), cancellationToken);
            context.Response.Headers.ETag = $"\"{rowVersion}\"";
            return Results.NoContent();
        }).RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().RequireRole("Author", "Admin"));
*/
    }
}

public sealed record LifecycleRequest(string RowVersion);
