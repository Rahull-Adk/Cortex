using Cortex.Application.Common;

namespace Cortex.Application.Auth;

public static class AuthErrors
{
    public static readonly Error EmailAlreadyRegistered = Error.Conflict("This email is already registered.");
    public static readonly Error InvalidCredentials = Error.Unauthorized("Invalid email or password.");
    public static readonly Error EmailNotVerified = Error.Forbidden("Verify your email address before signing in.");
    public static readonly Error InvalidGoogleToken = Error.Unauthorized("The Google identity token is invalid or expired.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Invalid or expired refresh token.");
    public static readonly Error InvalidVerificationToken = Error.Validation("Invalid or expired verification token.");
    public static readonly Error EmailNotRegistered = Error.NotFound("No account is registered with this email.");
    public static readonly Error EmailAlreadyVerified = Error.Conflict("This email is already verified.");
    public static readonly Error UserNotFound = Error.Unauthorized("User not found.");
}
