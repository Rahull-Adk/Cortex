using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using Cortex.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Options;

namespace Cortex.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly JwtSettings _jwtSettings;

    public AuthController(AuthService authService, IOptions<JwtSettings> jwtSettings)
    {
        _authService = authService;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return result.IsFailure ? MapError(result) : Ok(new { message = "Registration successful. Verify your email to sign in.", user = result.Value });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (result.IsFailure) return MapError(result);
        SetTokenCookies(result.Value);
        return Ok(result.Value);
    }

    [HttpPost("google-session")]
    public async Task<IActionResult> GoogleSession([FromBody] GoogleSessionRequest request, CancellationToken ct)
    {
        var result = await _authService.GoogleSessionAsync(request, ct);
        if (result.IsFailure) return MapError(result);
        SetTokenCookies(result.Value);
        return Ok(result.Value);
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(request.Token, ct);
        if (result.IsFailure) return MapError(result);
        SetTokenCookies(result.Value);
        return Ok(result.Value);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        await _authService.ResendVerificationAsync(request, ct);
        return Ok(new { message = "If the email exists and is unverified, a verification email has been sent." });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(request, ct);
        if (result.IsFailure) return MapError(result);
        SetTokenCookies(result.Value);
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("cortex_access_token", new CookieOptions { Path = "/", Secure = Request.IsHttps, SameSite = SameSiteMode.Lax });
        Response.Cookies.Delete("cortex_refresh_token", new CookieOptions { Path = "/", Secure = Request.IsHttps, SameSite = SameSiteMode.Lax });
        return Ok(new { message = "Logged out." });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var result = await _authService.GetCurrentUserAsync(userId, ct);
        return result.IsFailure ? Unauthorized() : Ok(result.Value);
    }

    private IActionResult MapError(Result result) => result.ErrorCode switch
    {
        AuthErrors.EmailAlreadyRegistered => Conflict(new { code = result.ErrorCode, message = result.ErrorMessage }),
        AuthErrors.InvalidCredentials => Unauthorized(new { code = result.ErrorCode, message = result.ErrorMessage }),
        AuthErrors.EmailNotVerified => StatusCode(StatusCodes.Status403Forbidden, new { code = result.ErrorCode, message = result.ErrorMessage }),
        AuthErrors.InvalidToken => BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage }),
        _ => StatusCode(500, new { message = "An unexpected error occurred." }),
    };

    private void SetTokenCookies(AuthTokensDto tokens)
    {
        var accessCookie = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
        };
        var refreshCookie = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
        };

        Response.Cookies.Append("cortex_access_token", tokens.AccessToken, accessCookie);
        Response.Cookies.Append("cortex_refresh_token", tokens.RefreshToken, refreshCookie);
    }
}

