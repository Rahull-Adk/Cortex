using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[Tags("Invitations")]
public class InvitationsController : ApiControllerBase
{
    private readonly InvitationService _invitations;

    public InvitationsController(InvitationService invitations) => _invitations = invitations;

    [Authorize]
    [HttpPost("api/organizations/{organizationId:guid}/invitations")]
    [EndpointSummary("Invite someone by email")]
    [EndpointDescription("Owner and Admin only. Emails an invitation link that lasts 7 days and works once. The person does not need an account yet. Inviting the same email again sends a fresh link and cancels the old one. The role can be Admin or Member.")]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status201Created, "application/json", Description = "Invitation created and emailed.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member, or the requested role is Owner.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The email already belongs to a member, or it was invited at the same moment by someone else (retry).")]
    public async Task<IActionResult> Invite(
        Guid organizationId,
        [FromBody] InviteMemberRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.InviteAsync(userId, organizationId, request, ct);
        return FromResult(result, invitation => StatusCode(StatusCodes.Status201Created, invitation));
    }

    [Authorize]
    [HttpGet("api/organizations/{organizationId:guid}/invitations")]
    [EndpointSummary("List pending invitations")]
    [EndpointDescription("Owner and Admin only. Invitations not accepted yet, including expired ones: compare expiresAt with the current time to label them.")]
    [ProducesResponseType<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK, "application/json", Description = "Pending invitations.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.ListAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [Authorize]
    [HttpDelete("api/organizations/{organizationId:guid}/invitations/{invitationId:guid}")]
    [EndpointSummary("Cancel an invitation")]
    [EndpointDescription("Owner and Admin only. The emailed link stops working.")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Cancelled.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business or the invitation does not exist, or the caller is not a member.")]
    public async Task<IActionResult> Revoke(Guid organizationId, Guid invitationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.RevokeAsync(userId, organizationId, invitationId, ct);
        return FromResult(result, NoContent);
    }

    [AllowAnonymous]
    [HttpGet("api/invitations/preview")]
    [EndpointSummary("Preview an invitation (public)")]
    [EndpointDescription("No sign-in needed. Call it on the accept page with the token from the link to show the business, who invited whom and the role. Then send a signed-out person to sign in or register with the invited email.")]
    [ProducesResponseType<InvitationPreviewDto>(StatusCodes.Status200OK, "application/json", Description = "Invitation details.")]
    [ProducesProblem(StatusCodes.Status400BadRequest, Description = "The link is invalid, expired, cancelled or already used.")]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        var result = await _invitations.PreviewAsync(token, ct);
        return FromResult(result, Ok);
    }

    [Authorize]
    [HttpPost("api/invitations/accept")]
    [EndpointSummary("Accept an invitation")]
    [EndpointDescription("For a signed-in user. Adds the caller to the business with the invited role and returns the business. A person without an account should use POST /api/auth/register-invited instead.")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK, "application/json", Description = "Joined. Open the returned business.")]
    [ProducesValidationProblem(Description = "The link is invalid, expired, cancelled or already used.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is signed in with a different email than the one invited.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The caller is already a member of this business.")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.AcceptAsync(userId, request, ct);
        return FromResult(result, Ok);
    }
}
