using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Cortex.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private const string AccessTokenCookie = "cortex_access_token";
    private const string RefreshTokenCookie = "cortex_refresh_token";

    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

    [HttpPost("register")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return FromResult(result, user => StatusCode(StatusCodes.Status201Created, user));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return FromResult(result, StartSession);
    }

    [HttpPost("google-session")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleSession([FromBody] GoogleSessionRequest request, CancellationToken ct)
    {
        var result = await _authService.GoogleSessionAsync(request, ct);
        return FromResult(result, StartSession);
    }

    [HttpGet("verify-email")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    public async Task<IActionResult> VerifyEmail([FromQuery] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(request.Token, ct);
        return FromResult(result, StartSession);
    }

    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        var result = await _authService.ResendVerificationAsync(request, ct);
        return FromResult(result, NoContent);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request,
        CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(ResolveRefreshToken(request?.RefreshToken), ct);
        if (result.IsFailure)
            ClearTokenCookies();

        return FromResult(result, StartSession);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request,
        CancellationToken ct)
    {
        await _authService.LogoutAsync(ResolveRefreshToken(request?.RefreshToken), ct);
        ClearTokenCookies();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _authService.GetCurrentUserAsync(userId, ct);
        return FromResult(result, Ok);
    }

    private IActionResult StartSession(AuthTokensDto tokens)
    {
        SetTokenCookies(tokens);
        return Ok(tokens);
    }

    private string? ResolveRefreshToken(string? bodyToken) =>
        string.IsNullOrWhiteSpace(bodyToken) ? Request.Cookies[RefreshTokenCookie] : bodyToken;

    private void SetTokenCookies(AuthTokensDto tokens)
    {
        Response.Cookies.Append(AccessTokenCookie, tokens.AccessToken, BuildCookieOptions(tokens.AccessTokenExpiresAt));
        Response.Cookies.Append(RefreshTokenCookie, tokens.RefreshToken, BuildCookieOptions(tokens.RefreshTokenExpiresAt));
    }

    private void ClearTokenCookies()
    {
        Response.Cookies.Delete(AccessTokenCookie, BuildCookieOptions());
        Response.Cookies.Delete(RefreshTokenCookie, BuildCookieOptions());
    }

    private CookieOptions BuildCookieOptions(DateTime? expiresAtUtc = null) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expiresAtUtc is null ? null : new DateTimeOffset(expiresAtUtc.Value, TimeSpan.Zero),
    };
}
