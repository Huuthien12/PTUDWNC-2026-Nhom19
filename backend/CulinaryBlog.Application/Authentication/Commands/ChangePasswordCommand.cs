using CulinaryBlog.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands;

public sealed record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<ChangePasswordResult>;

public sealed record ChangePasswordResult(bool Succeeded, string? ErrorCode, DateTimeOffset? LockoutEnd = null);

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty();
        RuleFor(command => command.NewPassword).NotEmpty();
        RuleFor(command => command.ConfirmNewPassword).NotEmpty();
        RuleFor(command => command.ConfirmNewPassword)
            .Equal(command => command.NewPassword)
            .WithMessage("New password confirmation must match.");
    }
}

public sealed class ChangePasswordCommandHandler(IIdentityService identity)
    : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    public async Task<ChangePasswordResult> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await identity.ChangePasswordAsync(command.UserId, command.CurrentPassword,
            command.NewPassword, cancellationToken);
        return new(result.Succeeded, result.ErrorCode, result.LockoutEnd);
    }
}
