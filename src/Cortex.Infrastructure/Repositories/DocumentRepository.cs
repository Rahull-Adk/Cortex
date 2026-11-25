using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly CortexDbContext _db;

    public DocumentRepository(CortexDbContext db) => _db = db;

    public Task<Document?> FindByIdAsync(Guid organizationId, Guid documentId, CancellationToken ct = default) =>
        _db.Documents.FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.Id == documentId, ct);

    public async Task<IReadOnlyList<Document>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Documents
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public void Add(Document document) => _db.Documents.Add(document);

    public void Remove(Document document) => _db.Documents.Remove(document);
}
