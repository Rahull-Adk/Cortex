using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly CortexDbContext _db;

    public OrganizationRepository(CortexDbContext db) => _db = db;

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default) =>
        _db.Organizations.AnyAsync(o => o.Slug == slug, ct);

    public void Add(Organization organization) => _db.Organizations.Add(organization);
}
