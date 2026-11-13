namespace Cortex.Application.Interfaces;

public sealed record StorageUsage(int DocumentCount, long StorageBytes);

public interface IUsageRepository
{
    // Atomic: false when the period allowance is already used up.
    Task<bool> TryConsumeAiAnswerAsync(
        Guid organizationId, DateTime periodStart, DateTime periodEnd, int limit, CancellationToken ct = default);

    Task RefundAiAnswerAsync(Guid organizationId, DateTime periodStart, CancellationToken ct = default);
    Task<int> GetAiAnswersUsedAsync(Guid organizationId, DateTime periodStart, CancellationToken ct = default);

    // Atomic: false when the document would exceed either limit.
    Task<bool> TryReserveStorageAsync(
        Guid organizationId, long bytes, int maxDocuments, long maxStorageBytes, CancellationToken ct = default);

    Task ReleaseStorageAsync(Guid organizationId, long bytes, CancellationToken ct = default);
    Task<StorageUsage> GetStorageAsync(Guid organizationId, CancellationToken ct = default);
}
