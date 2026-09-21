using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        IIdentityService identityService,
        JwtTokenService jwtTokenService)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Problem(
                type: "VALIDATION_ERROR",
                title: "Validation failed.",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Email and password are required.");
        }

        var result = await _identityService.LoginAsync(
            request.Email.Trim(),
            request.Password,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Problem(
                type: result.ErrorCode ?? "INVALID_CREDENTIALS",
                title: "Login failed.",
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "Email or password is incorrect.");
        }

        var accessToken =
            await _jwtTokenService.GenerateAccessTokenAsync(result);

        return Ok(new LoginResponse(
            accessToken,
            "Bearer"));
    }
}

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType);