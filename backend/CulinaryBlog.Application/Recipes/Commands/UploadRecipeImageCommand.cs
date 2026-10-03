using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Images;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

// Identity and role come from the authenticated endpoint, never from the request body.
// File type/size/magic-byte rules are enforced by IFileStorageService (ImageFileValidator).
public sealed record UploadRecipeImageCommand(Guid RecipeId, Stream Content, string? ContentType,
    string? AltText, string RowVersion, string UserId, bool IsAdmin) : IRequest<RecipeImageDto>;

public sealed class UploadRecipeImageCommandValidator : AbstractValidator<UploadRecipeImageCommand>
{
    public UploadRecipeImageCommandValidator()
    {
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.AltText).MaximumLength(200);
        RuleFor(x => x.RowVersion).Must(RecipeRowVersion.IsValid).WithMessage("RowVersion is not valid.");
    }
}

public sealed class UploadRecipeImageCommandHandler(IUnitOfWork unitOfWork, IFileStorageService storage,
    IImageCleanupQueue cleanup, TimeProvider clock) : IRequestHandler<UploadRecipeImageCommand, RecipeImageDto>
{
    public async Task<RecipeImageDto> Handle(UploadRecipeImageCommand command, CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.LoadForMutationAsync(unitOfWork.Recipes, command.RecipeId,
            command.UserId, command.IsAdmin, cancellationToken);

        // Fail fast on a stale RowVersion before any bytes are written to object storage.
        var expected = RecipeRowVersion.Decode(command.RowVersion);
        recipe.EnsureCurrentRowVersion(expected);

        // 400 for invalid MIME/size/signature, 503 when MinIO is down; nothing is written to the DB then.
        var stored = await storage.StoreAsync(recipe.Id, command.Content, command.ContentType, cancellationToken);
        try
        {
            var image = recipe.CreateImage(stored.Url, command.AltText);
            unitOfWork.Recipes.AddImage(image);
            recipe.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            unitOfWork.Recipes.Update(recipe, expected);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return RecipeImageDto.From(image, recipe.RowVersion);
        }
        catch
        {
            // SRS FR-FILE A5: DB commit failed after upload -> the stored object is an orphan.
            RecipeImageAccess.TryEnqueueDelete(cleanup, stored.ObjectKey);
            throw;
        }
    }
}
