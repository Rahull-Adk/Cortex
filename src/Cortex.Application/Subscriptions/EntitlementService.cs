using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;

namespace Cortex.Application.Subscriptions;

public class EntitlementService
{
    private readonly IOrganizationSubscriptionRepository _subscriptions;
    private readonly IUsageRepository _usage;
    private readonly TimeProvider _clock;

    public EntitlementService(
        IOrganizationSubscriptionRepository subscriptions,
        IUsageRepository usage,
        TimeProvider clock)
    {
        _subscriptions = subscriptions;
        _usage = usage;
        _clock = clock;
    }

    public async Task<Result> CheckIngestionAsync(Guid organizationId, CancellationToken ct = default)
    {
        var access = await FindPaidAsync(organizationId, ct);
        return access.IsFailure ? access.Error : Result.Success();
    }

    // Counts the document against the plan. Call ReleaseUploadAsync if the upload fails or the document is deleted.
    public async Task<Result> TryReserveUploadAsync(Guid organizationId, long bytes, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        var access = await FindPaidAsync(organizationId, ct);
        if (access.IsFailure)
            return access.Error;

        var plan = access.Value.Plan;
        if (await _usage.TryReserveStorageAsync(organizationId, bytes, plan.MaxDocuments, plan.MaxStorageBytes, ct))
            return Result.Success();

        var used = await _usage.GetStorageAsync(organizationId, ct);
        return used.DocumentCount >= plan.MaxDocuments
            ? SubscriptionErrors.DocumentQuotaExceeded
            : SubscriptionErrors.StorageQuotaExceeded;
    }

    // Deleting knowledge is never gated, so this works without a subscription.
    public Task ReleaseUploadAsync(Guid organizationId, long bytes, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        return _usage.ReleaseStorageAsync(organizationId, bytes, ct);
    }

    // Reserve before generating, then call RefundAiAnswerAsync if no answer was completed.
    public async Task<Result> TryConsumeAiAnswerAsync(Guid organizationId, CancellationToken ct = default)
    {
        var access = await FindPaidAsync(organizationId, ct);
        if (access.IsFailure)
            return access.Error;

        var subscription = access.Value;
        var (periodStart, periodEnd) = UsagePeriod(subscription);

        return await _usage.TryConsumeAiAnswerAsync(
            organizationId, periodStart, periodEnd, subscription.Plan.AiAnswersPerPeriod, ct)
            ? Result.Success()
            : SubscriptionErrors.AiAnswerQuotaExceeded;
    }

    public async Task RefundAiAnswerAsync(Guid organizationId, CancellationToken ct = default)
    {
        var subscription = await _subscriptions.FindByOrganizationAsync(organizationId, ct);
        if (subscription is null)
            return;

        await _usage.RefundAiAnswerAsync(organizationId, UsagePeriod(subscription).Start, ct);
    }

    internal static (DateTime Start, DateTime End) UsagePeriod(OrganizationSubscription subscription)
    {
        var start = subscription.CurrentPeriodStart ?? subscription.CreatedAt;
        return (start, subscription.CurrentPeriodEnd ?? start.AddMonths(1));
    }

    private async Task<Result<OrganizationSubscription>> FindPaidAsync(Guid organizationId, CancellationToken ct)
    {
        var subscription = await _subscriptions.FindByOrganizationAsync(organizationId, ct);
        if (subscription is null || !subscription.HasPaidAccess(_clock.GetUtcNow().UtcDateTime))
            return SubscriptionErrors.SubscriptionRequired;

        return subscription;
    }
}
