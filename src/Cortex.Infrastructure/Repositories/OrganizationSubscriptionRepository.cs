using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class OrganizationSubscriptionRepository : IOrganizationSubscriptionRepository
{
    private readonly CortexDbContext _db;

    public OrganizationSubscriptionRepository(CortexDbContext db) => _db = db;

    public Task<OrganizationSubscription?> FindByOrganizationAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.OrganizationSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

    public void Add(OrganizationSubscription subscription) => _db.OrganizationSubscriptions.Add(subscription);
}
