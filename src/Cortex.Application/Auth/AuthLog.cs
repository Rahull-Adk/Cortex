using Microsoft.Extensions.Logging;

namespace Cortex.Application.Auth;

// Identifiers only: never log emails, passwords or tokens.
internal static partial class AuthLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "User {UserId} registered with email and password.")]
    public static partial void UserRegistered(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "User {UserId} registered from invitation {InvitationId} and joined business {OrganizationId}.")]
    public static partial void InvitedUserRegistered(this ILogger logger, Guid userId, Guid invitationId, Guid organizationId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Invited registration rejected: {Reason}.")]
    public static partial void InvitedRegistrationRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Sign-in failed: no active account for the email.")]
    public static partial void SignInUnknownAccount(this ILogger logger);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Sign-in failed for user {UserId}: wrong password.")]
    public static partial void SignInWrongPassword(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Sign-in blocked for user {UserId}: email not verified.")]
    public static partial void SignInUnverified(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "User {UserId} signed in via {Method}.")]
    public static partial void SignedIn(this ILogger logger, Guid userId, string method);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "Google sign-in rejected: invalid ID token.")]
    public static partial void GoogleTokenRejected(this ILogger logger);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "Google account linked to user {UserId} (new user: {IsNewUser}).")]
    public static partial void GoogleAccountLinked(this ILogger logger, Guid userId, bool isNewUser);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Email verification failed: token invalid or expired.")]
    public static partial void VerificationTokenRejected(this ILogger logger);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Verification email re-sent to user {UserId}.")]
    public static partial void VerificationResent(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "Session refresh rejected: {Reason}.")]
    public static partial void RefreshRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Warning, Message = "Refresh token reuse for user {UserId}: session {FamilyId} revoked. The token may have been stolen.")]
    public static partial void RefreshTokenReused(this ILogger logger, Guid userId, Guid familyId);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Warning, Message = "Session {FamilyId} of user {UserId} revoked: {Reason}.")]
    public static partial void SessionRevoked(this ILogger logger, Guid userId, Guid familyId, string reason);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "User {UserId} signed out (session {FamilyId}).")]
    public static partial void SignedOut(this ILogger logger, Guid userId, Guid familyId);
}
