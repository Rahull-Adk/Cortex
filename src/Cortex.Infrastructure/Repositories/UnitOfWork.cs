using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cortex.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CortexDbContext _db;

    public UnitOfWork(CortexDbContext db) => _db = db;

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintException(ex);
        }
    }

    public void DiscardChanges() => _db.ChangeTracker.Clear();
}
