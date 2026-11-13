using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IOrganizationSubscriptionRepository
{
    // Includes Plan.
    Task<OrganizationSubscription?> FindByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    void Add(OrganizationSubscription subscription);
}
