using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations;
using Cortex.Domain.Entities;

namespace Cortex.Application.Authorization;

public class OrganizationAuthorizer
{
    private readonly IOrganizationMembershipRepository _memberships;

    public OrganizationAuthorizer(IOrganizationMembershipRepository memberships) => _memberships = memberships;

    public async Task<Result<OrganizationMembership>> AuthorizeAsync(
        Guid userId,
        Guid organizationId,
        Permission permission,
        CancellationToken ct = default)
    {
        var membership = await _memberships.FindActiveAsync(organizationId, userId, ct);

        // Non-members get the same answer as for a business that does not exist.
        if (membership is null)
            return OrganizationErrors.NotFound;

        if (!RolePermissions.Has(membership.Role, permission))
            return OrganizationErrors.Forbidden;

        return membership;
    }
}
