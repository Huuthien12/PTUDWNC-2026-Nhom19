using MediatR;

namespace CulinaryBlog.Application.Authentication.Commands.Register;

// DTO phản hồi kết quả đăng ký
public record RegisterResponse(
    bool Success, 
    string Message, 
    IEnumerable<string>? Errors = null
);

// Command yêu cầu đăng ký tài khoản
public record RegisterCommand(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword
) : IRequest<RegisterResponse>;