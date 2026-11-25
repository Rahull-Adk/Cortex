namespace Cortex.Application.Interfaces;

public sealed record PresignedUrl(string Url, DateTime ExpiresAt);

public sealed record StoredObject(long SizeBytes, string? ContentType);

public interface IFileStorage
{
    // The client must send exactly this Content-Type and Content-Length, or storage rejects the PUT.
    Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, long sizeBytes, CancellationToken ct = default);

    Task<PresignedUrl> CreateDownloadUrlAsync(string key, string fileName, CancellationToken ct = default);

    // Null when nothing is stored under the key.
    Task<StoredObject?> GetObjectAsync(string key, CancellationToken ct = default);

    // Succeeds when the key is already gone.
    Task DeleteAsync(string key, CancellationToken ct = default);
}
