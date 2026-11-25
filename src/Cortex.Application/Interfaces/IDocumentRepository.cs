using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IDocumentRepository
{
    Task<Document?> FindByIdAsync(Guid organizationId, Guid documentId, CancellationToken ct = default);

    // Newest first.
    Task<IReadOnlyList<Document>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default);

    void Add(Document document);
    void Remove(Document document);
}
