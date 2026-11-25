using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Documents;
using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.Application.Tests.Support;

internal sealed class DocumentFixture
{
    private readonly List<Document> _documents = [];

    public SubscriptionFixture Subs { get; } = new();
    public Mock<IDocumentRepository> Documents { get; } = new();
    public Mock<IFileStorage> Storage { get; } = new();

    public Dictionary<string, StoredObject> StoredObjects { get; } = [];
    public List<string> DeletedKeys { get; } = [];
    public IReadOnlyList<Document> StoredDocuments => _documents;

    public DocumentService Service { get; }

    public DocumentFixture()
    {
        Documents.Setup(r => r.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, Guid documentId, CancellationToken _) => _documents.FirstOrDefault(d =>
                d.OrganizationId == organizationId && d.Id == documentId));
        Documents.Setup(r => r.ListByOrganizationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, CancellationToken _) => _documents
                .Where(d => d.OrganizationId == organizationId)
                .OrderByDescending(d => d.CreatedAt)
                .ToList());
        Documents.Setup(r => r.Add(It.IsAny<Document>())).Callback<Document>(_documents.Add);
        Documents.Setup(r => r.Remove(It.IsAny<Document>())).Callback<Document>(d => _documents.Remove(d));

        Storage.Setup(s => s.CreateUploadUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, string _, long _, CancellationToken _) =>
                new PresignedUrl($"https://storage.test/{key}?upload", Subs.Now.AddMinutes(15)));
        Storage.Setup(s => s.CreateDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, string _, CancellationToken _) =>
                new PresignedUrl($"https://storage.test/{key}?download", Subs.Now.AddMinutes(5)));
        Storage.Setup(s => s.GetObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => StoredObjects.GetValueOrDefault(key));
        Storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) =>
            {
                DeletedKeys.Add(key);
                StoredObjects.Remove(key);
            })
            .Returns(Task.CompletedTask);

        var authorizer = new OrganizationAuthorizer(Subs.Orgs.Memberships.Object, NullLogger<OrganizationAuthorizer>.Instance);
        Service = new DocumentService(
            Documents.Object, Storage.Object, Subs.Orgs.UnitOfWork.Object, authorizer, Subs.EntitlementService,
            NullLogger<DocumentService>.Instance);
    }

    public void ClientUploads(Guid documentId, long sizeBytes, string contentType = "application/pdf")
    {
        var document = _documents.Single(d => d.Id == documentId);
        StoredObjects[document.S3Key] = new StoredObject(sizeBytes, contentType);
    }

    public void SaveHitsConcurrencyConflict() =>
        Subs.Orgs.UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException(new InvalidOperationException("row changed")));

    public void StorageDeleteFails() =>
        Storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("storage unreachable"));
}
