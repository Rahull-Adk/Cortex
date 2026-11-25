using Microsoft.Extensions.Logging;

namespace Cortex.Application.Documents;

// Identifiers only: file names can contain personal data.
internal static partial class DocumentLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Upload of document {DocumentId} ({ContentType}, {SizeBytes} bytes) started in business {OrganizationId} by user {UserId}.")]
    public static partial void UploadStarted(this ILogger logger, Guid organizationId, Guid documentId, Guid userId, string contentType, long sizeBytes);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Upload of document {DocumentId} in business {OrganizationId} completed.")]
    public static partial void UploadCompleted(this ILogger logger, Guid organizationId, Guid documentId);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Upload of document {DocumentId} in business {OrganizationId} rejected: {Reason}.")]
    public static partial void UploadRejected(this ILogger logger, Guid organizationId, Guid documentId, string reason);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "Document {DocumentId} in business {OrganizationId} deleted by user {UserId}.")]
    public static partial void DocumentDeleted(this ILogger logger, Guid organizationId, Guid documentId, Guid userId);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Warning, Message = "Stored file of document {DocumentId} in business {OrganizationId} could not be deleted and is orphaned.")]
    public static partial void StoredFileOrphaned(this ILogger logger, Exception exception, Guid organizationId, Guid documentId);
}
