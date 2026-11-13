using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly CortexDbContext _db;

    public PlanRepository(CortexDbContext db) => _db = db;

    public async Task<IReadOnlyList<Plan>> ListActiveAsync(CancellationToken ct = default) =>
        await _db.Plans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

    public Task<Plan?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Plans.FirstOrDefaultAsync(p => p.Code == code, ct);
}
