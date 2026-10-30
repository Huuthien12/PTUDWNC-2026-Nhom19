using System.Security.Cryptography;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Commands;

// Identity and role come from the authenticated endpoint, never from the request body.
// File type/size/magic-byte rules are enforced by IFileStorageService (ImageFileValidator).
public sealed record UploadRecipeImageCommand(Guid RecipeId, Guid UploadId, Stream Content, string? ContentType,
    string? AltText, string RowVersion, string UserId, bool IsAdmin) : IRequest<RecipeImageDto>;

public sealed class UploadRecipeImageCommandValidator : AbstractValidator<UploadRecipeImageCommand>
{
    public UploadRecipeImageCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.AltText).MaximumLength(200);
        RuleFor(x => x.RowVersion).Must(RecipeRowVersion.IsValid).WithMessage("RowVersion is not valid.");
    }
}

public sealed class UploadRecipeImageCommandHandler(IUnitOfWork unitOfWork, IFileStorageService storage,
    IRecipeImageOperationLock operationLock, IImageProcessingQueue processingQueue, TimeProvider clock,
    ILogger<UploadRecipeImageCommandHandler> logger,
    IRecipeCacheInvalidator? invalidator = null) : IRequestHandler<UploadRecipeImageCommand, RecipeImageDto>
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);

    public async Task<RecipeImageDto> Handle(UploadRecipeImageCommand command, CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.LoadForMutationAsync(unitOfWork.Recipes, command.RecipeId,
            command.UserId, command.IsAdmin, cancellationToken);

        var contentHash = await ComputeContentHashAsync(command.Content, cancellationToken);
        var normalizedAltText = NormalizeAltText(command.AltText);
        var expected = RecipeRowVersion.Decode(command.RowVersion);
        RecipeImageDto result;
        var shouldQueueProcessing = false;
        string? storedObjectKey = null;
        using (var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            operationTimeout.CancelAfter(OperationTimeout);
            var operationToken = operationTimeout.Token;
            await using (await operationLock.AcquireAsync(recipe.Id, operationToken))
            {
                var existingImage = await unitOfWork.Recipes.GetImageByIdAsync(command.UploadId, operationToken);
                if (existingImage is not null)
                {
                    EnsureSameUpload(recipe.Id, existingImage, command.UploadId, contentHash, normalizedAltText);
                    var currentRowVersion = await unitOfWork.Recipes.GetCurrentRowVersionAsync(
                        recipe.Id,
                        operationToken);
                    if (currentRowVersion is null)
                        throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe was not found.");
                    result = RecipeImageDto.From(existingImage, currentRowVersion);
                }
                else
                {
                    var currentRowVersion = await unitOfWork.Recipes.GetCurrentRowVersionAsync(
                        recipe.Id,
                        operationToken);
                    if (currentRowVersion is null)
                        throw new NotFoundException("RECIPE_NOT_FOUND", "Recipe was not found.");
                    if (!currentRowVersion.AsSpan().SequenceEqual(expected))
                        throw new ConcurrencyException(
                            "RECIPE_CONCURRENCY_CONFLICT",
                            "The recipe was changed by another request.");

                    // The lock is held from object upload through the recipe-row commit so cleanup
                    // cannot observe the object as unreferenced between these operations.
                    var stored = await storage.StoreAsync(
                        recipe.Id,
                        command.UploadId,
                        command.Content,
                        command.ContentType,
                        operationToken);
                    storedObjectKey = stored.ObjectKey;
                    try
                    {
                        var image = recipe.CreateImage(stored.Url, command.AltText);
                        image.Id = command.UploadId;
                        unitOfWork.Recipes.AddImage(image);
                        recipe.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                        unitOfWork.Recipes.Update(recipe, expected);
                        await unitOfWork.SaveChangesAsync(operationToken);
                        result = RecipeImageDto.From(image, recipe.RowVersion);
                        shouldQueueProcessing = true;
                    }
                    catch (Exception saveException)
                    {
                        using var verificationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        RecipeImage? replayedImage;
                        byte[]? committedRowVersion;
                        try
                        {
                            replayedImage = await unitOfWork.Recipes.GetImageByIdAsync(
                                command.UploadId,
                                verificationTimeout.Token);
                            committedRowVersion = replayedImage is null
                                ? null
                                : await unitOfWork.Recipes.GetCurrentRowVersionAsync(
                                    recipe.Id,
                                    verificationTimeout.Token);
                        }
                        catch (Exception verificationException)
                        {
                            logger.LogError(verificationException,
                                "Could not verify the outcome of image upload {UploadId}.", command.UploadId);
                            throw new AggregateException(
                                "The image upload result could not be verified.",
                                saveException,
                                verificationException);
                        }

                        if (replayedImage is not null && committedRowVersion is not null)
                        {
                            EnsureSameUpload(recipe.Id, replayedImage, command.UploadId, contentHash, normalizedAltText);
                            result = RecipeImageDto.From(replayedImage, committedRowVersion);
                        }
                        else
                        {
                            // Reuse of the object key remains safe; an uncommitted object is
                            // retained for a later retry or the grace-period orphan sweep.
                            throw;
                        }
                    }
                }
            }
        }

        if (shouldQueueProcessing)
        {
            try
            {
                if (storedObjectKey is null)
                    throw new InvalidOperationException("Committed upload has no stored object key.");
                processingQueue.EnqueueProcess(recipe.Id, result.ImageId, storedObjectKey);
            }
            catch (Exception exception)
            {
                // The upload is committed; the daily sweep retries missing variants after its grace period.
                logger.LogError(exception, "Could not enqueue image processing for recipe {RecipeId}, image {ImageId}.",
                    recipe.Id, result.ImageId);
            }
        }
        if (invalidator is not null) await invalidator.RecipeChangedAsync(recipe.Slug, cancellationToken);
        return result;
    }

    private static async Task<string> ComputeContentHashAsync(Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
            throw new BusinessRuleException("INVALID_IMAGE_FILE", "Image content must be seekable.");

        var originalPosition = content.Position;
        try
        {
            content.Position = 0;
            var hash = await SHA256.HashDataAsync(content, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private static string? NormalizeAltText(string? altText)
        => string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();

    private static void EnsureSameUpload(
        Guid recipeId,
        RecipeImage existingImage,
        Guid uploadId,
        string contentHash,
        string? normalizedAltText)
    {
        if (existingImage.IsDeleted ||
            existingImage.RecipeId != recipeId ||
            !RecipeImageStorageKey.TryGetUploadContentHash(
                existingImage.OriginalUrl,
                existingImage.RecipeId,
                uploadId,
                out var existingHash) ||
            !string.Equals(existingHash, contentHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existingImage.AltText, normalizedAltText, StringComparison.Ordinal))
            throw new ConflictException(
                "UPLOAD_ID_CONFLICT",
                "This uploadId has already been used with different image content or alt text.");
    }
}
