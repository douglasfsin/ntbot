using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Identity.Dtos;
using NtBot.Identity.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register/init")]
    [AllowAnonymous]
    public Task<AuthResponse> InitiateRegistration([FromBody] RegisterRequest request) =>
        _auth.InitiateRegistrationAsync(request);

    [HttpPost("register/verify")]
    [AllowAnonymous]
    public Task<AuthResponse> CompleteRegistration([FromBody] RegisterCompleteRequest request) =>
        _auth.CompleteRegistrationAsync(request);

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<AuthResponse> Login([FromBody] LoginRequest request) =>
        _auth.LoginAsync(request);

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public Task<AuthResponse> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        _auth.ForgotPasswordAsync(request);

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public Task<AuthResponse> ResetPassword([FromBody] ResetPasswordRequest request) =>
        _auth.ResetPasswordAsync(request);

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        var tenantId = User.FindFirst("tenant_id")?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.FindFirst("email")?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Ok(new { userId, tenantId, email, role });
    }
}
