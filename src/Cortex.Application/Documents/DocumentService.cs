using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Documents.Dtos;
using Cortex.Application.Interfaces;
using Cortex.Application.Subscriptions;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cortex.Application.Documents;

// Every document row holds one storage reservation, released only when the row is removed.
public class DocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAuthorizer _authorizer;
    private readonly EntitlementService _entitlements;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documents,
        IFileStorage storage,
        IUnitOfWork unitOfWork,
        OrganizationAuthorizer authorizer,
        EntitlementService entitlements,
        ILogger<DocumentService> logger)
    {
        _documents = documents;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
        _entitlements = entitlements;
        _logger = logger;
    }

    public async Task<Result<UploadSessionDto>> StartUploadAsync(
        Guid actorUserId,
        Guid organizationId,
        StartUploadRequest request,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.DocumentsManage, ct);
        if (access.IsFailure)
            return access.Error;

        var reservation = await _entitlements.TryReserveUploadAsync(organizationId, request.SizeBytes, ct);
        if (reservation.IsFailure)
            return reservation.Error;

        var document = Document.Create(
            organizationId, actorUserId, request.FileName, request.ContentType, request.SizeBytes);

        PresignedUrl upload;
        try
        {
            upload = await _storage.CreateUploadUrlAsync(document.S3Key, document.ContentType, document.SizeBytes, ct);
            _documents.Add(document);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            _unitOfWork.DiscardChanges();
            await _entitlements.ReleaseUploadAsync(organizationId, request.SizeBytes, CancellationToken.None);
            throw;
        }

        _logger.UploadStarted(organizationId, document.Id, actorUserId, document.ContentType, document.SizeBytes);

        var headers = new Dictionary<string, string> { ["Content-Type"] = document.ContentType };
        return new UploadSessionDto(ToDto(document), upload.Url, "PUT", headers, upload.ExpiresAt);
    }

    public async Task<Result<DocumentDto>> CompleteUploadAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid documentId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.DocumentsManage, ct);
        if (access.IsFailure)
            return access.Error;

        var document = await _documents.FindByIdAsync(organizationId, documentId, ct);
        if (document is null)
            return DocumentErrors.NotFound;

        if (document.Status == DocumentStatus.Failed)
            return DocumentErrors.UploadFailed;

        // Completing twice is harmless: the client may retry after a lost response.
        if (document.Status != DocumentStatus.AwaitingUpload)
            return ToDto(document);

        var stored = await _storage.GetObjectAsync(document.S3Key, ct);
        if (stored is null)
            return DocumentErrors.UploadNotReceived;

        if (stored.SizeBytes != document.SizeBytes)
        {
            _logger.UploadRejected(organizationId, document.Id, "stored size differs from the declared size");
            var removed = await RemoveAsync(document, ct);
            return removed.IsFailure ? removed.Error : DocumentErrors.UploadMismatch;
        }

        document.MarkUploaded(stored.SizeBytes);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _unitOfWork.DiscardChanges();
            return DocumentErrors.ConcurrentChange;
        }

        _logger.UploadCompleted(organizationId, document.Id);
        return ToDto(document);
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> ListAsync(
        Guid actorUserId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.DocumentsView, ct);
        if (access.IsFailure)
            return access.Error;

        var documents = await _documents.ListByOrganizationAsync(organizationId, ct);
        return documents.Select(ToDto).ToList();
    }

    public async Task<Result<DocumentDto>> GetAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid documentId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.DocumentsView, ct);
        if (access.IsFailure)
            return access.Error;

        var document = await _documents.FindByIdAsync(organizationId, documentId, ct);
        return document is null ? DocumentErrors.NotFound : ToDto(document);
    }

    public async Task<Result<DownloadLinkDto>> CreateDownloadLinkAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid documentId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.DocumentsView, ct);
        if (access.IsFailure)
            return access.Error;

        var document = await _documents.FindByIdAsync(organizationId, documentId, ct);
        if (document is null)
            return DocumentErrors.NotFound;

        if (document.Status is DocumentStatus.AwaitingUpload or DocumentStatus.Failed)
            return DocumentErrors.NotDownloadable;

        var link = await _storage.CreateDownloadUrlAsync(document.S3Key, document.FileName, ct);
        return new DownloadLinkDto(link.Url, link.ExpiresAt);
    }

    public async Task<Result> DeleteAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid documentId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.DocumentsManage, ct);
        if (access.IsFailure)
            return access.Error;

        var document = await _documents.FindByIdAsync(organizationId, documentId, ct);
        if (document is null)
            return DocumentErrors.NotFound;

        var removed = await RemoveAsync(document, ct);
        if (removed.IsFailure)
            return removed.Error;

        _logger.DocumentDeleted(organizationId, document.Id, actorUserId);
        return Result.Success();
    }

    // The row goes first, so no remaining row ever points at a missing file.
    private async Task<Result> RemoveAsync(Document document, CancellationToken ct)
    {
        _documents.Remove(document);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _unitOfWork.DiscardChanges();
            return DocumentErrors.ConcurrentChange;
        }

        await _entitlements.ReleaseUploadAsync(document.OrganizationId, document.SizeBytes, CancellationToken.None);

        try
        {
            await _storage.DeleteAsync(document.S3Key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.StoredFileOrphaned(ex, document.OrganizationId, document.Id);
        }

        return Result.Success();
    }

    private static DocumentDto ToDto(Document d) =>
        new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status, d.Error, d.UploadedByUserId, d.CreatedAt, d.UpdatedAt);
}
