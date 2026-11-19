using Cortex.Application.Authorization;
using Cortex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cortex.Application.Organizations;

// Identifiers only: never log emails or invitation tokens.
internal static partial class OrganizationLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Access denied: user {UserId} is not a member of business {OrganizationId} ({Permission}).")]
    public static partial void AccessDeniedNotMember(this ILogger logger, Guid userId, Guid organizationId, Permission permission);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Access denied: user {UserId} with role {Role} lacks {Permission} in business {OrganizationId}.")]
    public static partial void AccessDeniedRole(this ILogger logger, Guid userId, Guid organizationId, OrganizationRole role, Permission permission);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Business {OrganizationId} created by user {UserId}.")]
    public static partial void OrganizationCreated(this ILogger logger, Guid organizationId, Guid userId);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "Business creation by user {UserId} rejected: slug taken.")]
    public static partial void SlugTaken(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information, Message = "Business {OrganizationId} details updated by user {UserId}.")]
    public static partial void OrganizationUpdated(this ILogger logger, Guid organizationId, Guid userId);

    [LoggerMessage(EventId = 2111, Level = LogLevel.Information, Message = "User {ActorUserId} changed the role of user {TargetUserId} in business {OrganizationId} from {OldRole} to {NewRole}.")]
    public static partial void MemberRoleChanged(this ILogger logger, Guid organizationId, Guid actorUserId, Guid targetUserId, OrganizationRole oldRole, OrganizationRole newRole);

    [LoggerMessage(EventId = 2112, Level = LogLevel.Information, Message = "User {ActorUserId} removed user {TargetUserId} from business {OrganizationId}.")]
    public static partial void MemberRemoved(this ILogger logger, Guid organizationId, Guid actorUserId, Guid targetUserId);

    [LoggerMessage(EventId = 2113, Level = LogLevel.Information, Message = "User {UserId} left business {OrganizationId}.")]
    public static partial void MemberLeft(this ILogger logger, Guid organizationId, Guid userId);

    [LoggerMessage(EventId = 2114, Level = LogLevel.Information, Message = "User {ActorUserId} was blocked from changing the owner or assigning the Owner role in business {OrganizationId}.")]
    public static partial void OwnerChangeBlocked(this ILogger logger, Guid organizationId, Guid actorUserId);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "Invitation {InvitationId} to business {OrganizationId} sent by user {ActorUserId} as {Role} (renewed: {Renewed}).")]
    public static partial void InvitationSent(this ILogger logger, Guid organizationId, Guid invitationId, Guid actorUserId, OrganizationRole role, bool renewed);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "Invitation to business {OrganizationId} rejected: {Reason}.")]
    public static partial void InvitationRejected(this ILogger logger, Guid organizationId, string reason);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Information, Message = "Invitation {InvitationId} to business {OrganizationId} cancelled by user {ActorUserId}.")]
    public static partial void InvitationRevoked(this ILogger logger, Guid organizationId, Guid invitationId, Guid actorUserId);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information, Message = "Invitation {InvitationId} accepted: user {UserId} joined business {OrganizationId} as {Role}.")]
    public static partial void InvitationAccepted(this ILogger logger, Guid organizationId, Guid invitationId, Guid userId, OrganizationRole role);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Warning, Message = "User {UserId} tried to accept invitation {InvitationId}, which was sent to a different email.")]
    public static partial void InvitationEmailMismatch(this ILogger logger, Guid invitationId, Guid userId);

    [LoggerMessage(EventId = 2206, Level = LogLevel.Information, Message = "Invitation link rejected: invalid, expired or already used.")]
    public static partial void InvitationTokenRejected(this ILogger logger);
}
