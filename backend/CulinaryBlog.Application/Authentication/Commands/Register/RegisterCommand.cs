using MediatR;
using CulinaryBlog.Application.Authentication.DTOs;

namespace CulinaryBlog.Application.Authentication.Commands.Register;

public sealed record RegisterCommand(
    string FullName,
    string Email,
    string UserName,
    string Password)
    : IRequest<RegisterResult>;

public sealed record RegisterResult(AuthResponseDto? Tokens, string? ErrorCode);
