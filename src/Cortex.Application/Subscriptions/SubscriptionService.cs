using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Subscriptions.Dtos;
using Cortex.Domain.Entities;

namespace Cortex.Application.Subscriptions;

public class SubscriptionService
{
    private readonly IPlanRepository _plans;
    private readonly IOrganizationSubscriptionRepository _subscriptions;
    private readonly IOrganizationRepository _organizations;
    private readonly IUsageRepository _usage;
    private readonly IBillingProvider _billing;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAuthorizer _authorizer;
    private readonly TimeProvider _clock;

    public SubscriptionService(
        IPlanRepository plans,
        IOrganizationSubscriptionRepository subscriptions,
        IOrganizationRepository organizations,
        IUsageRepository usage,
        IBillingProvider billing,
        IUnitOfWork unitOfWork,
        OrganizationAuthorizer authorizer,
        TimeProvider clock)
    {
        _plans = plans;
        _subscriptions = subscriptions;
        _organizations = organizations;
        _usage = usage;
        _billing = billing;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken ct = default)
    {
        var plans = await _plans.ListActiveAsync(ct);
        return plans.Select(MapToDto).ToList();
    }

    public async Task<Result<SubscriptionDto>> GetAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(userId, organizationId, Permission.BillingView, ct);
        if (access.IsFailure)
            return access.Error;

        var storage = await _usage.GetStorageAsync(organizationId, ct);
        var subscription = await _subscriptions.FindByOrganizationAsync(organizationId, ct);

        if (subscription is null)
        {
            var free = await _plans.FindByCodeAsync(Plan.FreeCode, ct);
            if (free is null)
                return SubscriptionErrors.PlanNotFound;

            return new SubscriptionDto(
                MapToDto(free), null, false, null, null, null, false,
                new UsageDto(0, storage.DocumentCount, storage.StorageBytes));
        }

        var answersUsed = await _usage.GetAiAnswersUsedAsync(
            organizationId, EntitlementService.UsagePeriod(subscription).Start, ct);

        return new SubscriptionDto(
            MapToDto(subscription.Plan),
            subscription.Status,
            subscription.HasPaidAccess(_clock.GetUtcNow().UtcDateTime),
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.PaidThroughUtc,
            subscription.CancelAtPeriodEnd,
            new UsageDto(answersUsed, storage.DocumentCount, storage.StorageBytes));
    }

    public async Task<Result<BillingRedirectDto>> StartCheckoutAsync(
        Guid userId,
        Guid organizationId,
        StartCheckoutRequest request,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(userId, organizationId, Permission.BillingManage, ct);
        if (access.IsFailure)
            return access.Error;

        var plan = await _plans.FindByCodeAsync(request.PlanCode.Trim().ToLowerInvariant(), ct);
        if (plan is null || !plan.IsActive)
            return SubscriptionErrors.PlanNotFound;
        if (!plan.IsPaid)
            return SubscriptionErrors.PlanNotPurchasable;

        var subscription = await _subscriptions.FindByOrganizationAsync(organizationId, ct);
        if (subscription is { IsLive: true })
            return SubscriptionErrors.AlreadySubscribed;

        var url = await _billing.CreateCheckoutSessionAsync(
            new CheckoutSessionRequest(organizationId, plan.Code, subscription?.BillingCustomerRef), ct);

        return new BillingRedirectDto(url);
    }

    public async Task<Result<BillingRedirectDto>> OpenPortalAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(userId, organizationId, Permission.BillingManage, ct);
        if (access.IsFailure)
            return access.Error;

        var subscription = await _subscriptions.FindByOrganizationAsync(organizationId, ct);
        if (subscription?.BillingCustomerRef is null)
            return SubscriptionErrors.NoBillingAccount;

        var url = await _billing.CreatePortalSessionAsync(organizationId, subscription.BillingCustomerRef, ct);
        return new BillingRedirectDto(url);
    }

    public async Task<Result> HandleWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        var parsed = await _billing.ParseWebhookAsync(payload, signature, ct);
        if (parsed.IsFailure)
            return parsed.Error;

        var snapshot = parsed.Value.Snapshot;
        if (snapshot is null)
            return Result.Success();

        // Unknown plans and businesses are acknowledged so the provider stops retrying them.
        var plan = await _plans.FindByCodeAsync(snapshot.PlanCode, ct);
        if (plan is null)
            return Result.Success();

        var subscription = await _subscriptions.FindByOrganizationAsync(snapshot.OrganizationId, ct);
        if (subscription is null)
        {
            if (!await _organizations.ExistsAsync(snapshot.OrganizationId, ct))
                return Result.Success();

            subscription = OrganizationSubscription.Create(snapshot.OrganizationId, plan);
            _subscriptions.Add(subscription);
        }
        else if (!subscription.Accepts(snapshot.BillingSubscriptionRef, snapshot.Status))
        {
            return Result.Success();
        }

        subscription.ApplyProviderState(
            plan,
            snapshot.Status,
            snapshot.BillingCustomerRef,
            snapshot.BillingSubscriptionRef,
            snapshot.CurrentPeriodStart,
            snapshot.CurrentPeriodEnd,
            snapshot.CancelAtPeriodEnd,
            snapshot.PaidThroughUtc);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is UniqueConstraintException or ConcurrencyConflictException)
        {
            // Two events for the same business raced. The provider redelivers and the retry reads fresh state.
            _unitOfWork.DiscardChanges();
            return SubscriptionErrors.WebhookConflict;
        }

        return Result.Success();
    }

    private static PlanDto MapToDto(Plan plan) => new(
        plan.Code,
        plan.Name,
        plan.PriceMinor,
        plan.Currency,
        plan.AiAnswersPerPeriod,
        plan.MaxDocuments,
        plan.MaxStorageBytes);
}
