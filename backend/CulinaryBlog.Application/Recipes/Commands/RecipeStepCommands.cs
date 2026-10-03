using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands;

public sealed record CreateRecipeStepRequest(
    string Description,
    int? DurationMinutes = null,
    string? ImageUrl = null,
    string? RowVersion = null
);

public sealed record UpdateRecipeStepRequest(
    string Description,
    int? DurationMinutes = null,
    string? ImageUrl = null,
    string? RowVersion = null
);

public sealed record DeleteRecipeStepRequest(
    string RowVersion
);

public sealed record CreateRecipeStepCommand(
    Guid RecipeId,
    CreateRecipeStepRequest Request,
    string UserId,
    bool IsAdmin
) : IRequest<RecipeStepListDto>;

public sealed record UpdateRecipeStepCommand(
    Guid RecipeId,
    Guid StepId,
    UpdateRecipeStepRequest Request,
    string UserId,
    bool IsAdmin
) : IRequest<RecipeStepListDto>;

public sealed record DeleteRecipeStepCommand(
    Guid RecipeId,
    Guid StepId,
    string RowVersion,
    string UserId,
    bool IsAdmin
) : IRequest<string>;

public sealed class CreateRecipeStepCommandValidator
    : AbstractValidator<CreateRecipeStepCommand>
{
    public CreateRecipeStepCommandValidator()
    {
        RuleFor(x => x.Request.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.Request.DurationMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.DurationMinutes.HasValue);

        RuleFor(x => x.Request.RowVersion)
            .Must(IsCanonicalBase64)
            .WithMessage(
                "RowVersion is required and must be canonical Base64.");
    }

    private static bool IsCanonicalBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);

            return bytes.Length > 0
                && Convert.ToBase64String(bytes) == value;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UpdateRecipeStepCommandValidator
    : AbstractValidator<UpdateRecipeStepCommand>
{
    public UpdateRecipeStepCommandValidator()
    {
        RuleFor(x => x.Request.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.Request.DurationMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.DurationMinutes.HasValue);

        RuleFor(x => x.Request.RowVersion)
            .Must(IsCanonicalBase64)
            .WithMessage(
                "RowVersion is required and must be canonical Base64.");
    }

    private static bool IsCanonicalBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);

            return bytes.Length > 0
                && Convert.ToBase64String(bytes) == value;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class DeleteRecipeStepCommandValidator
    : AbstractValidator<DeleteRecipeStepCommand>
{
    public DeleteRecipeStepCommandValidator()
    {
        RuleFor(x => x.RowVersion)
            .Must(IsCanonicalBase64)
            .WithMessage(
                "RowVersion is required and must be canonical Base64.");
    }

    private static bool IsCanonicalBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);

            return bytes.Length > 0
                && Convert.ToBase64String(bytes) == value;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class CreateRecipeStepCommandHandler(
    IUnitOfWork unitOfWork,
    IValidator<CreateRecipeStepCommand> validator,
    TimeProvider clock)
    : IRequestHandler<CreateRecipeStepCommand, RecipeStepListDto>
{
    public async Task<RecipeStepListDto> Handle(
        CreateRecipeStepCommand command,
        CancellationToken cancellationToken)
    {
        var recipe =
            await unitOfWork.Recipes.GetForStepMutationAsync(
                command.RecipeId,
                cancellationToken)
            ?? throw new NotFoundException(
                "RECIPE_NOT_FOUND",
                "Recipe not found.");

        if (!command.IsAdmin &&
            recipe.AuthorId != command.UserId)
        {
            throw new ForbiddenException(
                "RECIPE_FORBIDDEN",
                "Only the author or an Admin may modify recipe steps.");
        }

        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var request = command.Request;

        var originalRowVersion =
            Convert.FromBase64String(request.RowVersion!);

        var nextStepNumber = recipe.Steps
            .Where(step => !step.IsDeleted)
            .Select(step => step.StepNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var step = new RecipeStep
        {
            RecipeId = recipe.Id,
            StepNumber = nextStepNumber,
            Description = request.Description,
            TimerMinutes = request.DurationMinutes,
            ImageUrl = request.ImageUrl
        };

        // Explicitly register the new RecipeStep as Added.
        // This prevents EF from treating the new Guid-keyed entity
        // as Modified and issuing UPDATE instead of INSERT.
        await unitOfWork.Recipes.AddStepAsync(
            step,
            cancellationToken);

        recipe.UpdatedAt =
            clock.GetUtcNow().UtcDateTime;

        unitOfWork.Recipes.Update(
            recipe,
            originalRowVersion);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return RecipeStepCommandResult.CreateResult(recipe);
    }
}

public sealed class UpdateRecipeStepCommandHandler(
    IUnitOfWork unitOfWork,
    IValidator<UpdateRecipeStepCommand> validator,
    TimeProvider clock)
    : IRequestHandler<UpdateRecipeStepCommand, RecipeStepListDto>
{
    public async Task<RecipeStepListDto> Handle(
        UpdateRecipeStepCommand command,
        CancellationToken cancellationToken)
    {
        var recipe =
            await unitOfWork.Recipes.GetForStepMutationAsync(
                command.RecipeId,
                cancellationToken)
            ?? throw new NotFoundException(
                "RECIPE_NOT_FOUND",
                "Recipe not found.");

        if (!command.IsAdmin &&
            recipe.AuthorId != command.UserId)
        {
            throw new ForbiddenException(
                "RECIPE_FORBIDDEN",
                "Only the author or an Admin may modify recipe steps.");
        }

        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var step = recipe.Steps
            .FirstOrDefault(x => x.Id == command.StepId);

        if (step is null)
        {
            throw new NotFoundException(
                "RECIPE_STEP_NOT_FOUND",
                "Recipe step not found.");
        }

        var request = command.Request;

        var originalRowVersion =
            Convert.FromBase64String(request.RowVersion!);

        step.Description = request.Description;
        step.TimerMinutes = request.DurationMinutes;
        step.ImageUrl = request.ImageUrl;
        step.UpdatedAt =
            clock.GetUtcNow().UtcDateTime;

        recipe.UpdatedAt =
            clock.GetUtcNow().UtcDateTime;

        unitOfWork.Recipes.Update(
            recipe,
            originalRowVersion);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return RecipeStepCommandResult.CreateResult(recipe);
    }
}

public sealed class DeleteRecipeStepCommandHandler(
    IUnitOfWork unitOfWork,
    IValidator<DeleteRecipeStepCommand> validator,
    TimeProvider clock)
    : IRequestHandler<DeleteRecipeStepCommand, string>
{
    public async Task<string> Handle(
    DeleteRecipeStepCommand command,
    CancellationToken cancellationToken)
{
    var recipe =
        await unitOfWork.Recipes.GetForStepMutationAsync(
            command.RecipeId,
            cancellationToken)
        ?? throw new NotFoundException(
            "RECIPE_NOT_FOUND",
            "Recipe not found.");

    if (!command.IsAdmin &&
        recipe.AuthorId != command.UserId)
    {
        throw new ForbiddenException(
            "RECIPE_FORBIDDEN",
            "Only the author or an Admin may modify recipe steps.");
    }

    await validator.ValidateAndThrowAsync(
        command,
        cancellationToken);

    var step = recipe.Steps
        .FirstOrDefault(x => x.Id == command.StepId);

    if (step is null)
    {
        throw new NotFoundException(
            "RECIPE_STEP_NOT_FOUND",
            "Recipe step not found.");
    }

    var originalRowVersion =
        Convert.FromBase64String(command.RowVersion);

    var now = clock.GetUtcNow().UtcDateTime;

    await unitOfWork.ExecuteInTransactionAsync(
        async transactionCancellationToken =>
        {
            // ---------------------------------------------------------
            // SAVE 1:
            // Soft-delete the selected step and update the parent
            // Recipe.RowVersion exactly once.
            //
            // The filtered unique index ignores this step after
            // IsDeleted becomes true.
            // ---------------------------------------------------------

            step.IsDeleted = true;
            step.UpdatedAt = now;

            recipe.UpdatedAt = now;

            unitOfWork.Recipes.Update(
                recipe,
                originalRowVersion);

            await unitOfWork.SaveChangesAsync(
                transactionCancellationToken);

            // ---------------------------------------------------------
            // SAVE 2:
            // Renumber the remaining active steps.
            //
            // This is now safe because the deleted step is no longer
            // included in the filtered unique index.
            //
            // Do NOT modify Recipe here, otherwise its RowVersion
            // would be advanced a second time.
            // ---------------------------------------------------------

            var remainingSteps = recipe.Steps
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.StepNumber)
                .ToList();

            for (var index = 0;
                 index < remainingSteps.Count;
                 index++)
            {
                remainingSteps[index].StepNumber = index + 1;
                remainingSteps[index].UpdatedAt = now;
            }

            await unitOfWork.SaveChangesAsync(
                transactionCancellationToken);
        },
        cancellationToken);

    return Convert.ToBase64String(recipe.RowVersion);
    }
}

internal static class RecipeStepCommandResult
{
    public static RecipeStepListDto CreateResult(
        Recipe recipe)
    {
        var items = recipe.Steps
            .Where(step => !step.IsDeleted)
            .OrderBy(step => step.StepNumber)
            .Select(step => new RecipeStepDto(
                step.Id,
                step.StepNumber,
                step.Description,
                step.TimerMinutes,
                step.ImageUrl))
            .ToList();

        return new RecipeStepListDto(
            items,
            Convert.ToBase64String(recipe.RowVersion));
    }
}
