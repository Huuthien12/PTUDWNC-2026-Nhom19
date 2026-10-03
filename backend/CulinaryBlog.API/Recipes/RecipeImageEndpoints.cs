using System.Security.Claims;
using CulinaryBlog.Application.Recipes.Commands;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Recipes;

// SRS 8.5 / FR-RCP-008. The current Recipe RowVersion (Base64) is sent in the If-Match header
// or, when the header is absent, in the "rowVersion" field (multipart form for POST, JSON body otherwise).
// Every success response returns the Recipe's NEW RowVersion in the body and in the ETag header.
public static class RecipeImageEndpoints
{
    public static void MapRecipeImageEndpoints(this WebApplication app)
    {
        var images = app.MapGroup("/api/v1/recipes/{id:guid}/images")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser().RequireRole("Author", "Admin"));

        images.MapPost("", UploadAsync);
        images.MapPatch("{imageId:guid}/primary", SetPrimaryAsync);
        images.MapDelete("{imageId:guid}", DeleteAsync);
    }

    private static async Task<IResult> UploadAsync(Guid id, HttpContext http, ClaimsPrincipal user,
        ISender sender, CancellationToken cancellationToken)
    {
        var userId = RequireUserId(user);
        var request = http.Request;
        if (!request.HasFormContentType)
            throw Invalid("file", "Request must be multipart/form-data with a \"file\" field.");

        IFormCollection form;
        try { form = await request.ReadFormAsync(cancellationToken); }
        catch (InvalidDataException) { throw Invalid("file", "Malformed multipart/form-data body."); }

        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            throw Invalid("file", "An image file is required in the \"file\" field.");

        var rowVersion = ResolveRowVersion(request, form["rowVersion"].ToString());
        var altText = form["altText"].ToString();

        await using var content = file.OpenReadStream();
        var result = await sender.Send(new UploadRecipeImageCommand(id, content, file.ContentType,
            string.IsNullOrWhiteSpace(altText) ? null : altText, rowVersion, userId, user.IsInRole("Admin")),
            cancellationToken);

        SetETag(http, result.RowVersion);
        return Results.Created($"/api/v1/recipes/{id}/images/{result.ImageId}", result);
    }

    private static async Task<IResult> SetPrimaryAsync(Guid id, Guid imageId, [FromBody] ImageMutationRequest? body,
        HttpContext http, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var userId = RequireUserId(user);
        var rowVersion = ResolveRowVersion(http.Request, body?.RowVersion);
        var result = await sender.Send(new SetPrimaryRecipeImageCommand(id, imageId, rowVersion, userId,
            user.IsInRole("Admin")), cancellationToken);

        SetETag(http, result.RowVersion);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteAsync(Guid id, Guid imageId, [FromBody] ImageMutationRequest? body,
        HttpContext http, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var userId = RequireUserId(user);
        var rowVersion = ResolveRowVersion(http.Request, body?.RowVersion);
        var result = await sender.Send(new DeleteRecipeImageCommand(id, imageId, rowVersion, userId,
            user.IsInRole("Admin")), cancellationToken);

        SetETag(http, result.RowVersion);
        return Results.NoContent();
    }

    private static string RequireUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId) ? throw new UnauthorizedAccessException() : userId;
    }

    // If-Match: "<base64>" (quotes and a weak W/ prefix are tolerated) wins over the body field.
    private static string ResolveRowVersion(HttpRequest request, string? bodyValue)
    {
        var raw = request.Headers["If-Match"].ToString();
        if (string.IsNullOrWhiteSpace(raw)) raw = bodyValue;
        if (string.IsNullOrWhiteSpace(raw))
            throw Invalid("rowVersion", "The current Recipe RowVersion is required (If-Match header or rowVersion).");

        raw = raw.Trim();
        if (raw.StartsWith("W/", StringComparison.Ordinal)) raw = raw[2..];
        return raw.Trim('"');
    }

    private static void SetETag(HttpContext http, string rowVersion)
        => http.Response.Headers["ETag"] = $"\"{rowVersion}\"";

    private static ValidationException Invalid(string field, string message)
        => new ValidationException(new[] { new ValidationFailure(field, message) });
}

public sealed record ImageMutationRequest(string? RowVersion);
