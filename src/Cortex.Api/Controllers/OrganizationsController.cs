using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[Authorize]
[Route("api/organizations")]
[Tags("Businesses")]
[ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
public class OrganizationsController : ApiControllerBase
{
    private readonly OrganizationService _organizations;
    private readonly MemberService _members;

    public OrganizationsController(OrganizationService organizations, MemberService members)
    {
        _organizations = organizations;
        _members = members;
    }

    [HttpPost]
    [EndpointSummary("Create a business")]
    [EndpointDescription("Creates a business (organization) and makes the caller its Owner. slug is optional: 3-100 lowercase letters, numbers and hyphens, derived from the name when omitted. Creating a business is free.")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status201Created, "application/json", Description = "Business created. The caller is the Owner.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The slug is already taken.")]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _organizations.CreateAsync(userId, request, ct);
        return FromResult(result, organization =>
            CreatedAtAction(nameof(Get), new { organizationId = organization.Id }, organization));
    }

    [HttpGet]
    [EndpointSummary("List my businesses")]
    [EndpointDescription("Businesses the caller belongs to, each with the caller's role in it. Use it for the business switcher.")]
    [ProducesResponseType<IReadOnlyList<OrganizationDto>>(StatusCodes.Status200OK, "application/json", Description = "The caller's businesses (may be empty).")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return Ok(await _organizations.ListMineAsync(userId, ct));
    }

    [HttpGet("{organizationId:guid}")]
    [EndpointSummary("Get a business")]
    [EndpointDescription("Any member can view it. The response includes the caller's role.")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK, "application/json", Description = "The business.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _organizations.GetAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpPut("{organizationId:guid}")]
    [EndpointSummary("Update business details")]
    [EndpointDescription("Changes the name and logo URL. Owner and Admin only.")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK, "application/json", Description = "The updated business.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member, which cannot edit the business.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    public async Task<IActionResult> Update(
        Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _organizations.UpdateAsync(userId, organizationId, request, ct);
        return FromResult(result, Ok);
    }

    [HttpGet("{organizationId:guid}/members")]
    [EndpointSummary("List members")]
    [EndpointDescription("All members with their roles. Any member can view. Pending invitations are listed separately (GET .../invitations).")]
    [ProducesResponseType<IReadOnlyList<MemberDto>>(StatusCodes.Status200OK, "application/json", Description = "The members.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    public async Task<IActionResult> ListMembers(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _members.ListAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpPut("{organizationId:guid}/members/{memberUserId:guid}")]
    [EndpointSummary("Change a member's role")]
    [EndpointDescription("Owner and Admin only. The role can be Admin or Member; Owner cannot be assigned. Takes effect on the member's next request.")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status200OK, "application/json", Description = "The member with the new role.")]
    [ProducesValidationProblem(Description = "The role is not Admin or Member.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member, or the target is the Owner (the Owner's role cannot be changed).")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist, the caller is not a member, or the user is not a member.")]
    public async Task<IActionResult> ChangeMemberRole(
        Guid organizationId,
        Guid memberUserId,
        [FromBody] ChangeMemberRoleRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _members.ChangeRoleAsync(userId, organizationId, memberUserId, request, ct);
        return FromResult(result, Ok);
    }

    [HttpDelete("{organizationId:guid}/members/{memberUserId:guid}")]
    [EndpointSummary("Remove a member, or leave the business")]
    [EndpointDescription("With another user's ID: removes them (Owner and Admin only). With the caller's own ID: leaves the business (any member except the Owner).")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Removed.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "A Member tried to remove someone else, or the target is the Owner (the Owner cannot be removed or leave).")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist, the caller is not a member, or the user is not a member.")]
    public async Task<IActionResult> RemoveMember(Guid organizationId, Guid memberUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _members.RemoveAsync(userId, organizationId, memberUserId, ct);
        return FromResult(result, NoContent);
    }
}
