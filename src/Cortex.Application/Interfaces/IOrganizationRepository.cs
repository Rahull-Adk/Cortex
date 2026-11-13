using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<bool> ExistsAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    void Add(Organization organization);
}
