using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands;

public sealed record GetCurrentUserQuery(string UserId) : IRequest<UserProfileDto>;

public sealed class GetCurrentUserQueryHandler(IIdentityService identity)
    : IRequestHandler<GetCurrentUserQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken) =>
        ToDto(await identity.GetProfileAsync(query.UserId, cancellationToken)
            ?? throw new NotFoundException("USER_NOT_FOUND", "User not found."));

    internal static UserProfileDto ToDto(UserProfileResult user) => new(user.Id, user.FullName, user.Email,
        user.UserName, user.AvatarUrl, user.Roles, user.EmailConfirmed, user.CreatedAt);
}

public sealed record UpdateProfileCommand(string UserId, string? FullName, string? AvatarUrl)
    : IRequest<UserProfileDto>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => command.FullName is not null || command.AvatarUrl is not null)
            .WithMessage("At least one editable profile field is required.");
        When(command => command.FullName is not null, () =>
            RuleFor(command => command.FullName!).Must(name => name.Trim().Length is >= 2 and <= 100));
        When(command => command.AvatarUrl is not null, () =>
            RuleFor(command => command.AvatarUrl!).Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("AvatarUrl must be an absolute URL."));
    }
}

public sealed class UpdateProfileCommandHandler(IIdentityService identity)
    : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(UpdateProfileCommand command, CancellationToken cancellationToken) =>
        GetCurrentUserQueryHandler.ToDto(await identity.UpdateProfileAsync(
            command.UserId, command.FullName, command.AvatarUrl, cancellationToken)
            ?? throw new NotFoundException("USER_NOT_FOUND", "User not found."));
}
