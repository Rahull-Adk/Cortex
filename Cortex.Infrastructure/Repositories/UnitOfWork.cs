using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Persistence;

namespace Cortex.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CortexDbContext _db;

    public UnitOfWork(CortexDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
