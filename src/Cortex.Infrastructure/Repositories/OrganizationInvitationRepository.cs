using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class OrganizationInvitationRepository : IOrganizationInvitationRepository
{
    private readonly CortexDbContext _db;

    public OrganizationInvitationRepository(CortexDbContext db) => _db = db;

    public Task<OrganizationInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.InvitedBy)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

    public Task<OrganizationInvitation?> FindByEmailAsync(Guid organizationId, string email, CancellationToken ct = default) =>
        _db.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.OrganizationId == organizationId && i.Email == email, ct);

    public Task<OrganizationInvitation?> FindByIdAsync(Guid organizationId, Guid invitationId, CancellationToken ct = default) =>
        _db.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.OrganizationId == organizationId && i.Id == invitationId, ct);

    public async Task<IReadOnlyList<OrganizationInvitation>> ListByOrganizationAsync(
        Guid organizationId, CancellationToken ct = default) =>
        await _db.OrganizationInvitations
            .AsNoTracking()
            .Include(i => i.InvitedBy)
            .Where(i => i.OrganizationId == organizationId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

    public void Add(OrganizationInvitation invitation) => _db.OrganizationInvitations.Add(invitation);

    public void Remove(OrganizationInvitation invitation) => _db.OrganizationInvitations.Remove(invitation);
}
