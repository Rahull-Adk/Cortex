using Cortex.Domain.Enums;

namespace Cortex.Domain.Entities;

public class Document
{
    private Document() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string S3Key { get; private set; } = default!;
    public DocumentStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Document Create(
        Guid organizationId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long sizeBytes)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("A document requires an organization.", nameof(organizationId));
        if (uploadedByUserId == Guid.Empty)
            throw new ArgumentException("A document requires an uploader.", nameof(uploadedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var name = fileName.Trim();

        return new Document
        {
            Id = id,
            OrganizationId = organizationId,
            UploadedByUserId = uploadedByUserId,
            FileName = name,
            ContentType = contentType.Trim().ToLowerInvariant(),
            SizeBytes = sizeBytes,
            S3Key = BuildS3Key(organizationId, id, name),
            Status = DocumentStatus.AwaitingUpload,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // The client-declared size is replaced by what S3 actually stored.
    public void MarkUploaded(long actualSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actualSizeBytes);
        EnsureStatus(DocumentStatus.AwaitingUpload);

        SizeBytes = actualSizeBytes;
        Transition(DocumentStatus.Pending);
    }

    public void MarkProcessing()
    {
        if (Status == DocumentStatus.Processing)
            return;

        EnsureStatus(DocumentStatus.Pending);
        Transition(DocumentStatus.Processing);
    }

    public void MarkReady()
    {
        if (Status == DocumentStatus.Ready)
            return;

        EnsureStatus(DocumentStatus.Pending, DocumentStatus.Processing);
        Error = null;
        Transition(DocumentStatus.Ready);
    }

    public void MarkAsFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        EnsureStatus(DocumentStatus.AwaitingUpload, DocumentStatus.Pending, DocumentStatus.Processing);

        Error = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        Transition(DocumentStatus.Failed);
    }

    private void Transition(DocumentStatus next)
    {
        Status = next;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureStatus(params DocumentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new InvalidOperationException($"Document {Id} cannot leave status {Status} this way.");
    }

    private static string BuildS3Key(Guid organizationId, Guid documentId, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension.Length is < 2 or > 10 ||!extension.Skip(1).All(char.IsAsciiLetterOrDigit))
            extension = string.Empty;

        return $"orgs/{organizationId}/documents/{documentId}{extension}";
    }
}
