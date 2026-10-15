using CulinaryBlog.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Authentication.Commands;

public sealed record ForgotPasswordCommand(string Email) : IRequest;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(command => command.Email)
        .NotEmpty()
        .EmailAddress();
}

public sealed class ForgotPasswordCommandHandler(
    IIdentityService identity,
    IPasswordResetEmailQueue emails,
    ILogger<ForgotPasswordCommandHandler> logger) : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var reset = await identity.GeneratePasswordResetTokenAsync(command.Email.Trim(), cancellationToken);
        if (reset is null) return;

        if (!emails.TryEnqueue(new(reset.Email, reset.FullName, reset.Token)))
            logger.LogWarning("Password reset email queue is full; the message was dropped.");
    }
}

public sealed record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<ResetPasswordResult>;

public sealed record ResetPasswordResult(bool Succeeded, string? ErrorCode);

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Token).NotEmpty().MaximumLength(4096);
        RuleFor(command => command.NewPassword).NotEmpty();
        RuleFor(command => command.ConfirmNewPassword)
            .NotEmpty()
            .Equal(command => command.NewPassword)
            .WithMessage("New password confirmation must match.");
    }
}

public sealed class ResetPasswordCommandHandler(
    IIdentityService identity,
    IRefreshTokenStore refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    public async Task<ResetPasswordResult> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        IdentityPasswordResetResult? result = null;
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            result = await identity.ResetPasswordAsync(
                command.Email.Trim(), command.Token, command.NewPassword, transactionCancellationToken);
            if (!result.Succeeded) return;

            await refreshTokens.RevokeAllAsync(
                result.UserId!, clock.GetUtcNow().UtcDateTime, transactionCancellationToken);
        }, cancellationToken);

        return new(result!.Succeeded, result.ErrorCode);
    }
}
