using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IOrganizationMembershipRepository
{
    // Includes Organization. Null when the organization or the user is inactive.
    Task<OrganizationMembership?> FindActiveAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    // Includes User.
    Task<OrganizationMembership?> FindWithUserAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationMembership>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    void Add(OrganizationMembership membership);
    void Remove(OrganizationMembership membership);
}
