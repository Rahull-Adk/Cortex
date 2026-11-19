using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Cortex.Api.Controllers;

[Route("api/auth")]
[Tags("Auth")]
public class AuthController : ApiControllerBase
{
    private const string AccessTokenCookie = "cortex_access_token";
    private const string RefreshTokenCookie = "cortex_refresh_token";

    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

    [HttpPost("register")]
    [EndpointSummary("Register with email and password")]
    [EndpointDescription("Creates an unverified account and emails a verification link. The user is NOT signed in yet: show a check-your-email screen. They get signed in when they open the link (GET /api/auth/verify-email).")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created, "application/json", Description = "Account created. The email still has to be verified.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The email is already registered.")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return FromResult(result, user => StatusCode(StatusCodes.Status201Created, user));
    }

    [HttpPost("register-invited")]
    [EndpointSummary("Register from an invitation link")]
    [EndpointDescription("For an invited person who has no account yet. Send the invitation token with a name and password. The account is created with the invited email (already verified), joins the business with the invited role and is signed in. No separate accept call is needed.")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json", Description = "Account created, joined the business and signed in.")]
    [ProducesValidationProblem(Description = "Invalid fields, or the invitation is invalid, expired, cancelled or already used.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "A verified account already uses the invited email. Sign in and call POST /api/invitations/accept instead.")]
    public async Task<IActionResult> RegisterInvited([FromBody] RegisterInvitedRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterInvitedAsync(request, ct);
        return FromResult(result, StartSession);
    }

    [HttpPost("login")]
    [EndpointSummary("Sign in with email and password")]
    [EndpointDescription("Returns the session tokens and also sets the cortex_access_token and cortex_refresh_token HttpOnly cookies.")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json", Description = "Signed in.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Wrong email or password.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The email is not verified yet. Offer to resend the verification email.")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return FromResult(result, StartSession);
    }

    [HttpPost("google-session")]
    [EndpointSummary("Sign in with Google")]
    [EndpointDescription("Exchanges the Google ID token from NextAuth (account.id_token, not the NextAuth session token) for a Cortex session. Creates the account on first sign-in. Sets the same cookies as login.")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json", Description = "Signed in.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "The Google ID token is invalid, expired or issued for another client ID.")]
    public async Task<IActionResult> GoogleSession([FromBody] GoogleSessionRequest request, CancellationToken ct)
    {
        var result = await _authService.GoogleSessionAsync(request, ct);
        return FromResult(result, StartSession);
    }

    [HttpGet("verify-email")]
    [EndpointSummary("Verify email from the emailed link")]
    [EndpointDescription("Call it with the token from the verification link. Marks the email as verified and signs the user in, so redirect straight into the app.")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json", Description = "Email verified and signed in.")]
    [ProducesValidationProblem(Description = "The token is missing, invalid or expired. Offer to resend the email.")]
    public async Task<IActionResult> VerifyEmail([FromQuery] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(request.Token, ct);
        return FromResult(result, StartSession);
    }

    [HttpPost("resend-verification")]
    [EndpointSummary("Resend the verification email")]
    [EndpointDescription("Sends a new verification link. Earlier links stop working.")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Email sent.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "No account uses this email.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The email is already verified. Send the user to sign in.")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        var result = await _authService.ResendVerificationAsync(request, ct);
        return FromResult(result, NoContent);
    }

    [HttpPost("refresh")]
    [EndpointSummary("Refresh the session")]
    [EndpointDescription("Rotates the session. Send no body to use the cortex_refresh_token cookie, or send the refresh token in the body. Each refresh token works ONCE: always store the new one. Reusing an old token revokes the whole session, so keep only one refresh in flight per session (deduplicate across tabs).")]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK, "application/json", Description = "New access and refresh tokens. The cookies are updated too.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "The refresh token is invalid, expired or already used. The cookies are cleared: send the user to sign in.")]
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
    [EndpointSummary("Sign out")]
    [EndpointDescription("Revokes the session of the refresh token (from the cookie or the body) and clears the cookies. Needs no access token and always succeeds.")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Signed out.")]
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
    [EndpointSummary("Get the signed-in user")]
    [EndpointDescription("Returns the profile of the user the access token belongs to.")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK, "application/json", Description = "The current user.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
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
