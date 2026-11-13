using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class UsageRepository : IUsageRepository
{
    private readonly CortexDbContext _db;

    public UsageRepository(CortexDbContext db) => _db = db;

    public async Task<bool> TryConsumeAiAnswerAsync(
        Guid organizationId, DateTime periodStart, DateTime periodEnd, int limit, CancellationToken ct = default)
    {
        if (limit <= 0)
            return false;

        if (await IncrementAiAnswersAsync(organizationId, periodStart, limit, ct))
            return true;

        // Either the allowance is used up or the period has no row yet.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO ai_usage_periods (id, organization_id, period_start, period_end, answers_used)
            VALUES ({Guid.NewGuid()}, {organizationId}, {periodStart}, {periodEnd}, 0)
            ON CONFLICT (organization_id, period_start) DO NOTHING
            """,
            ct);

        return await IncrementAiAnswersAsync(organizationId, periodStart, limit, ct);
    }

    public Task RefundAiAnswerAsync(Guid organizationId, DateTime periodStart, CancellationToken ct = default) =>
        _db.AiUsagePeriods
            .Where(p => p.OrganizationId == organizationId && p.PeriodStart == periodStart && p.AnswersUsed > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AnswersUsed, p => p.AnswersUsed - 1), ct);

    public Task<int> GetAiAnswersUsedAsync(Guid organizationId, DateTime periodStart, CancellationToken ct = default) =>
        _db.AiUsagePeriods
            .Where(p => p.OrganizationId == organizationId && p.PeriodStart == periodStart)
            .Select(p => p.AnswersUsed)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryReserveStorageAsync(
        Guid organizationId, long bytes, int maxDocuments, long maxStorageBytes, CancellationToken ct = default)
    {
        if (maxDocuments <= 0 || bytes > maxStorageBytes)
            return false;

        if (await AddDocumentAsync(organizationId, bytes, maxDocuments, maxStorageBytes, ct))
            return true;

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO organization_storage_usage (organization_id, document_count, storage_bytes)
            VALUES ({organizationId}, 0, 0)
            ON CONFLICT (organization_id) DO NOTHING
            """,
            ct);

        return await AddDocumentAsync(organizationId, bytes, maxDocuments, maxStorageBytes, ct);
    }

    public Task ReleaseStorageAsync(Guid organizationId, long bytes, CancellationToken ct = default) =>
        _db.OrganizationStorageUsage
            .Where(u => u.OrganizationId == organizationId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.DocumentCount, u => u.DocumentCount > 0 ? u.DocumentCount - 1 : 0)
                    .SetProperty(u => u.StorageBytes, u => u.StorageBytes > bytes ? u.StorageBytes - bytes : 0),
                ct);

    public async Task<StorageUsage> GetStorageAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.OrganizationStorageUsage
            .Where(u => u.OrganizationId == organizationId)
            .Select(u => new StorageUsage(u.DocumentCount, u.StorageBytes))
            .FirstOrDefaultAsync(ct)
        ?? new StorageUsage(0, 0);

    private async Task<bool> IncrementAiAnswersAsync(
        Guid organizationId, DateTime periodStart, int limit, CancellationToken ct) =>
        await _db.AiUsagePeriods
            .Where(p => p.OrganizationId == organizationId && p.PeriodStart == periodStart && p.AnswersUsed < limit)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AnswersUsed, p => p.AnswersUsed + 1), ct) == 1;

    private async Task<bool> AddDocumentAsync(
        Guid organizationId, long bytes, int maxDocuments, long maxStorageBytes, CancellationToken ct) =>
        await _db.OrganizationStorageUsage
            .Where(u => u.OrganizationId == organizationId
                && u.DocumentCount < maxDocuments
                && u.StorageBytes + bytes <= maxStorageBytes)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.DocumentCount, u => u.DocumentCount + 1)
                    .SetProperty(u => u.StorageBytes, u => u.StorageBytes + bytes),
                ct) == 1;
}
