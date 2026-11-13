using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IOrganizationInvitationRepository
{
    // Includes Organization and InvitedBy.
    Task<OrganizationInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<OrganizationInvitation?> FindByEmailAsync(Guid organizationId, string email, CancellationToken ct = default);
    Task<OrganizationInvitation?> FindByIdAsync(Guid organizationId, Guid invitationId, CancellationToken ct = default);

    // Includes InvitedBy.
    Task<IReadOnlyList<OrganizationInvitation>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default);

    void Add(OrganizationInvitation invitation);
    void Remove(OrganizationInvitation invitation);
}
