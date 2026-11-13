using Cortex.Application.Common;

namespace Cortex.Application.Organizations;

public static class OrganizationErrors
{
    public static readonly Error NotFound = Error.NotFound("Business not found.");
    public static readonly Error Forbidden = Error.Forbidden("You do not have permission to perform this action.");
    public static readonly Error SlugTaken = Error.Conflict("This slug is already in use.");
    public static readonly Error UserNotFound = Error.Unauthorized("User not found.");
    public static readonly Error AlreadyMember = Error.Conflict("This user is already a member of the business.");
    public static readonly Error MemberNotFound = Error.NotFound("Member not found.");
    public static readonly Error InvitationNotFound = Error.NotFound("Invitation not found.");
    public static readonly Error InvalidInvitation = Error.Validation("This invitation is invalid or has expired.");
    public static readonly Error InvitationForAnotherEmail = Error.Forbidden("This invitation was sent to a different email address.");
    public static readonly Error InvitationConflict = Error.Conflict("This email was just invited. Try again.");
    public static readonly Error OwnerImmutable = Error.Forbidden("The owner's membership cannot be changed or removed.");
}
