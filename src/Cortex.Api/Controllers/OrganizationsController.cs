using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[Authorize]
[Route("api/organizations")]
[ProducesProblem(StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _organizations.CreateAsync(userId, request, ct);
        return FromResult(result, organization =>
            CreatedAtAction(nameof(Get), new { organizationId = organization.Id }, organization));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationDto>>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return Ok(await _organizations.ListMineAsync(userId, ct));
    }

    [HttpGet("{organizationId:guid}")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _organizations.GetAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpPut("{organizationId:guid}")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType<IReadOnlyList<MemberDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _members.ListAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpPost("{organizationId:guid}/members")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(
        Guid organizationId,
        [FromBody] AddMemberRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _members.AddAsync(userId, organizationId, request, ct);
        return FromResult(result, member => StatusCode(StatusCodes.Status201Created, member));
    }

    [HttpPut("{organizationId:guid}/members/{memberUserId:guid}")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid organizationId, Guid memberUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _members.RemoveAsync(userId, organizationId, memberUserId, ct);
        return FromResult(result, NoContent);
    }
}
