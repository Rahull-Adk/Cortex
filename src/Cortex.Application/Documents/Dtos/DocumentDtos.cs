using Cortex.Domain.Enums;

namespace Cortex.Application.Documents.Dtos;

public sealed record StartUploadRequest(string FileName, string ContentType, long SizeBytes);

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    string? Error,
    Guid UploadedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UploadSessionDto(
    DocumentDto Document,
    string UploadUrl,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    DateTime ExpiresAt);

public sealed record DownloadLinkDto(string Url, DateTime ExpiresAt);
