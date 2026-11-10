using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    void Add(Organization organization);
}
