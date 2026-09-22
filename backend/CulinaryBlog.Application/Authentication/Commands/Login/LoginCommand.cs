using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password)
    : IRequest<LoginResult>;

public sealed record LoginResult(
    bool Succeeded,
    string? AccessToken,
    string TokenType,
    string? ErrorCode);
