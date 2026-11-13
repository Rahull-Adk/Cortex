using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

public class InvitationsController : ApiControllerBase
{
    private readonly InvitationService _invitations;

    public InvitationsController(InvitationService invitations) => _invitations = invitations;

    [Authorize]
    [HttpPost("api/organizations/{organizationId:guid}/invitations")]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
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
    [ProducesResponseType<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.ListAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [Authorize]
    [HttpDelete("api/organizations/{organizationId:guid}/invitations/{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid organizationId, Guid invitationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.RevokeAsync(userId, organizationId, invitationId, ct);
        return FromResult(result, NoContent);
    }

    [AllowAnonymous]
    [HttpGet("api/invitations/preview")]
    [ProducesResponseType<InvitationPreviewDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromQuery] string token, CancellationToken ct)
    {
        var result = await _invitations.PreviewAsync(token, ct);
        return FromResult(result, Ok);
    }

    [Authorize]
    [HttpPost("api/invitations/accept")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _invitations.AcceptAsync(userId, request, ct);
        return FromResult(result, Ok);
    }
}
