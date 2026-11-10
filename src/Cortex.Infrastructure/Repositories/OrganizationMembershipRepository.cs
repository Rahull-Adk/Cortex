using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class OrganizationMembershipRepository : IOrganizationMembershipRepository
{
    private readonly CortexDbContext _db;

    public OrganizationMembershipRepository(CortexDbContext db) => _db = db;

    public Task<OrganizationMembership?> FindActiveAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
        _db.OrganizationMemberships
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId
                    && m.UserId == userId
                    && m.Organization.IsActive
                    && m.User.IsActive,
                ct);

    public Task<OrganizationMembership?> FindWithUserAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
        _db.OrganizationMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<OrganizationMembership>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _db.OrganizationMemberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId && m.Organization.IsActive)
            .OrderBy(m => m.Organization.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.OrganizationMemberships
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

    public void Add(OrganizationMembership membership) => _db.OrganizationMemberships.Add(membership);

    public void Remove(OrganizationMembership membership) => _db.OrganizationMemberships.Remove(membership);
}
