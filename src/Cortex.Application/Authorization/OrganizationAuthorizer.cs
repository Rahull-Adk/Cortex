using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations;
using Cortex.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Cortex.Application.Authorization;

public class OrganizationAuthorizer
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly ILogger<OrganizationAuthorizer> _logger;

    public OrganizationAuthorizer(IOrganizationMembershipRepository memberships, ILogger<OrganizationAuthorizer> logger)
    {
        _memberships = memberships;
        _logger = logger;
    }

    public async Task<Result<OrganizationMembership>> AuthorizeAsync(
        Guid userId,
        Guid organizationId,
        Permission permission,
        CancellationToken ct = default)
    {
        var membership = await _memberships.FindActiveAsync(organizationId, userId, ct);

        // Non-members get the same answer as for a business that does not exist.
        if (membership is null)
        {
            _logger.AccessDeniedNotMember(userId, organizationId, permission);
            return OrganizationErrors.NotFound;
        }

        if (!RolePermissions.Has(membership.Role, permission))
        {
            _logger.AccessDeniedRole(userId, organizationId, membership.Role, permission);
            return OrganizationErrors.Forbidden;
        }

        return membership;
    }
}
